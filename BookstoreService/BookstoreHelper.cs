using Common.Models;
using Microsoft.ServiceFabric.Data.Collections;

namespace BookstoreService
{
	internal static class BookstoreHelper
	{
		public static async Task<Book> getBookById(Microsoft.ServiceFabric.Data.ITransaction tx, IReliableDictionary<string, Book> books, string bookId)
		{
			var book = await books.TryGetValueAsync(tx, bookId);

			if (!book.HasValue)
			{
				throw new ArgumentException($"Book with {bookId} doesn't exists!");
			}

			return book.Value;
		}
	}
}
