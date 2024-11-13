using System.Diagnostics;
using System.Fabric;
using Common.Interfaces;
using Common.Models;
using Microsoft.ServiceFabric.Data.Collections;
using Microsoft.ServiceFabric.Services.Communication.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Remoting.V2.FabricTransport.Runtime;
using Microsoft.ServiceFabric.Services.Runtime;

namespace BookstoreService
{
    internal sealed class BookstoreService(StatefulServiceContext context) : StatefulService(context), IBookstore
    {
        private const string BOOK_DICTIONARY = "books";
        private const string RESERVED_BOOK_DICTIONARY = "reserved_books";

        private IReliableDictionary<string, Book>? _books;
        private IReliableDictionary<Guid, ReservedBook>? _reservedBooks;

        public async Task Commit(Guid transactionId)
        {
			_books = await StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>(BOOK_DICTIONARY);

			_reservedBooks = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>(RESERVED_BOOK_DICTIONARY);

			using var tx = StateManager.CreateTransaction();

			var reservedBookResult = await _reservedBooks.TryGetValueAsync(tx, transactionId);

			if (reservedBookResult.HasValue)
			{
				ReservedBook reservedBook = reservedBookResult.Value;

				var bookResult = await _books.TryGetValueAsync(tx, reservedBook.BookId);

				if (bookResult.HasValue)
				{
					Book book = bookResult.Value;

					book.Quantity -= reservedBook.Quantity;

					await _books.SetAsync(tx, reservedBook.BookId, book);

					await _reservedBooks.TryRemoveAsync(tx, transactionId);

					await tx.CommitAsync();
				}
			}
		}

		public async Task<bool> Prepare(Guid transactionId)
		{
            bool isPrepared = false;

			_books = await StateManager.GetOrAddAsync<IReliableDictionary<string, Book>>(BOOK_DICTIONARY);

			_reservedBooks = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>(RESERVED_BOOK_DICTIONARY);

			using (var tx = StateManager.CreateTransaction())
			{

				var reservedBookResult = await _reservedBooks.TryGetValueAsync(tx, transactionId);

                if (reservedBookResult.HasValue)
                {
                    ReservedBook reservedBook = reservedBookResult.Value;

					var bookResult = await _books.TryGetValueAsync(tx, reservedBook.BookId);

                    if (bookResult.HasValue)
                    {
                        Book book = bookResult.Value;
						isPrepared = reservedBook.Quantity <= book.Quantity;
					}
				}
			}

            return isPrepared;
		}

        public async Task Rollback(Guid transactionId)
        {
            _reservedBooks = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>(RESERVED_BOOK_DICTIONARY);

			using var tx = StateManager.CreateTransaction();

			await _reservedBooks.TryRemoveAsync(tx, transactionId);

			await tx.CommitAsync();
		}

		public async Task EnlistPurchase(Guid transactionId, string bookID, uint count)
        {
            _reservedBooks = await StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>(RESERVED_BOOK_DICTIONARY);

			using var tx = StateManager.CreateTransaction();

			await _reservedBooks.SetAsync(tx, transactionId, new ReservedBook() { Quantity = count, BookId = bookID });

			await tx.CommitAsync();
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
			_reservedBooks = await this.StateManager.GetOrAddAsync<IReliableDictionary<Guid, ReservedBook>>(RESERVED_BOOK_DICTIONARY);

			using var tx = this.StateManager.CreateTransaction();

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
