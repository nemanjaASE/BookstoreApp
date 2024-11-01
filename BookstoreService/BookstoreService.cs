using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Fabric;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Interfaces;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;
using Models;

namespace BookstoreService
{
    internal sealed class BookstoreService : StatefulService, IBookstore
    {
        private const string BOOK_DICTIONARY = "books";
        private const string RESERVED_BOOK_DICTIONARY = "reserved_books";

        private IReliableDictionary<string, Book> _books;
        private IReliableDictionary<string, uint> _reservedBooks;

        public BookstoreService(StatefulServiceContext context)
            : base(context)
        {
        }

        public async Task Commit()
        {
			var stateManager = this.StateManager;

			_books = await stateManager.GetOrAddAsync<IReliableDictionary<string, Book>>(BOOK_DICTIONARY);
			_reservedBooks = await stateManager.GetOrAddAsync<IReliableDictionary<string, uint>>(RESERVED_BOOK_DICTIONARY);
     
			using (var tx = stateManager.CreateTransaction())
			{
				var reservedBookEnumerator = (await _reservedBooks.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

				while (await reservedBookEnumerator.MoveNextAsync(CancellationToken.None))
				{
                    string bookId = reservedBookEnumerator.Current.Key;
                    uint reservedQuantity = reservedBookEnumerator.Current.Value;

                    Book book = await BookstoreHelper.getBookById(tx, _books, bookId);

                    book.Quantity -= reservedQuantity;

                    await _books.SetAsync(tx, reservedBookEnumerator.Current.Key, book);
				}

				await _reservedBooks.ClearAsync();

				await tx.CommitAsync();
			}
		}

		public async Task<bool> Prepare()
		{
			var stateManager = this.StateManager;

			_books = await stateManager.GetOrAddAsync<IReliableDictionary<string, Book>>(BOOK_DICTIONARY);
			_reservedBooks = await stateManager.GetOrAddAsync<IReliableDictionary<string, uint>>(RESERVED_BOOK_DICTIONARY);

			using (var tx = stateManager.CreateTransaction())
			{
				var reservedBookEnumerator = (await _reservedBooks.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

				while (await reservedBookEnumerator.MoveNextAsync(CancellationToken.None))
				{
                    string bookId = reservedBookEnumerator.Current.Key;

                    Book book = await BookstoreHelper.getBookById(tx, _books, bookId);

					if (reservedBookEnumerator.Current.Value >= book.Quantity)
					{
						return false;
					}
				}

				return true;
			}
		}

        public async Task Rollback()
        {
            var stateManager = this.StateManager;

            _reservedBooks = await stateManager.GetOrAddAsync<IReliableDictionary<string, uint>>(RESERVED_BOOK_DICTIONARY);

            using (var tx = stateManager.CreateTransaction())
            {
                await _reservedBooks.ClearAsync();
                await tx.CommitAsync();
            }
        }

		public async Task EnlistPurchase(string bookID, uint count)
        {
            var stateManeger = this.StateManager;
            _reservedBooks = await stateManeger.GetOrAddAsync<IReliableDictionary<string, uint>>(RESERVED_BOOK_DICTIONARY);

            using (var tx = stateManeger.CreateTransaction())
            {
				var reservedQuantity = await _reservedBooks.TryGetValueAsync(tx, bookID);
				uint newReservedQuantity = reservedQuantity.HasValue ? reservedQuantity.Value + count : count;

                await _reservedBooks.SetAsync(tx, bookID, newReservedQuantity);

				await tx.CommitAsync();
            }
        }

        public async Task<double> GetItemPrice(string bookId)
        {
			var stateManager = this.StateManager;

            _books = await stateManager.GetOrAddAsync<IReliableDictionary<string, Book>>(BOOK_DICTIONARY);
            double bookPrice = 0;

            using (var tx = stateManager.CreateTransaction())
            {
                Book book = await BookstoreHelper.getBookById(tx, _books, bookId);

                bookPrice = book.Price;
            }

            return bookPrice;
		}

        public async Task<Dictionary<string, Book>> ListAvailableItems()
        {
            var stateManager = this.StateManager;

            var availableBooks = new Dictionary<string, Book>();

            _books = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>(BOOK_DICTIONARY);

            using (var tx = stateManager.CreateTransaction())
            {
               var enumerator = (await _books.CreateEnumerableAsync(tx)).GetAsyncEnumerator();

               while (await enumerator.MoveNextAsync(CancellationToken.None))
               {
                    if(enumerator.Current.Value.Quantity > 0)
                        availableBooks.Add(enumerator.Current.Key, enumerator.Current.Value);
               }
            }

            return availableBooks;
        }

        protected override IEnumerable<ServiceReplicaListener> CreateServiceReplicaListeners()
        {
            return new List<ServiceReplicaListener>
            {
                new ServiceReplicaListener(serviceContext =>
                    new FabricTransportServiceRemotingListener(
                        serviceContext,
                        this, new FabricTransportRemotingListenerSettings
                            {
                                ExceptionSerializationTechnique = FabricTransportRemotingListenerSettings.ExceptionSerialization.Default,
                            })
                    )
            };
        }

        protected override async Task RunAsync(CancellationToken cancellationToken)
        {
            _books = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>(BOOK_DICTIONARY);
			_reservedBooks = await this.StateManager.GetOrAddAsync<IReliableDictionary<string, uint>>(RESERVED_BOOK_DICTIONARY);

			using (var tx = this.StateManager.CreateTransaction())
            {
                var enumerator = (await _books.CreateEnumerableAsync(tx)).GetAsyncEnumerator();
                if (!await enumerator.MoveNextAsync(cancellationToken))
                {
                    Debug.WriteLine("---Uspesno inicijalizovani podaci!---");
                    await _books.AddAsync(tx, "book1", new Book { Title = "Book 1", Quantity = 5, Price = 100 });
                    await _books.AddAsync(tx, "book2", new Book { Title = "Book 2", Quantity = 1, Price = 50 });
                    await _books.AddAsync(tx, "book3", new Book { Title = "Book 3", Quantity = 0, Price = 200 });
                }

                await _reservedBooks.ClearAsync();

				await tx.CommitAsync();
			}
        }
    }
    }
