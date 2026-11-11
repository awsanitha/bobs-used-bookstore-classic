using Bookstore.Domain.Books;
using Bookstore.Domain.ReferenceData;
using Microsoft.EntityFrameworkCore;
using System;

namespace Bookstore.Data
{
    /// <summary>
    /// Seeds initial reference data and books via EF Core's HasData mechanism.
    /// Fixed dates are used so the model snapshot remains stable across restarts.
    /// </summary>
    public static class BookstoreDataSeeder
    {
        private static readonly DateTime SeedDate = new DateTime(2024, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        public static void Seed(ModelBuilder modelBuilder)
        {
            SeedReferenceData(modelBuilder);
            SeedBooks(modelBuilder);
        }

        private static void SeedReferenceData(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<ReferenceDataItem>().HasData(
                CreateRef(1, ReferenceDataType.BookType, "Hardcover"),
                CreateRef(2, ReferenceDataType.BookType, "Trade Paperback"),
                CreateRef(3, ReferenceDataType.BookType, "Mass Market Paperback"),

                CreateRef(4, ReferenceDataType.Condition, "New"),
                CreateRef(5, ReferenceDataType.Condition, "Like New"),
                CreateRef(6, ReferenceDataType.Condition, "Good"),
                CreateRef(7, ReferenceDataType.Condition, "Acceptable"),

                CreateRef(8, ReferenceDataType.Genre, "Biographies"),
                CreateRef(9, ReferenceDataType.Genre, "Children's Books"),
                CreateRef(10, ReferenceDataType.Genre, "History"),
                CreateRef(11, ReferenceDataType.Genre, "Literature & Fiction"),
                CreateRef(12, ReferenceDataType.Genre, "Mystery, Thriller & Suspense"),
                CreateRef(13, ReferenceDataType.Genre, "Science Fiction & Fantasy"),
                CreateRef(14, ReferenceDataType.Genre, "Travel"),

                CreateRef(15, ReferenceDataType.Publisher, "Arcadia Books"),
                CreateRef(16, ReferenceDataType.Publisher, "Astral Publishing"),
                CreateRef(17, ReferenceDataType.Publisher, "Moonlight Publishing"),
                CreateRef(18, ReferenceDataType.Publisher, "Dreamscape Press"),
                CreateRef(19, ReferenceDataType.Publisher, "Enchanted Library"),
                CreateRef(20, ReferenceDataType.Publisher, "Fantasia House"),
                CreateRef(21, ReferenceDataType.Publisher, "Horizon Books"),
                CreateRef(22, ReferenceDataType.Publisher, "Infinity Press"),
                CreateRef(23, ReferenceDataType.Publisher, "Paradigm Publishing"),
                CreateRef(24, ReferenceDataType.Publisher, "Aurora Publishing")
            );
        }

        private static ReferenceDataItem CreateRef(int id, ReferenceDataType dataType, string text)
        {
            return new ReferenceDataItem(dataType, text)
            {
                Id = id,
                CreatedOn = SeedDate,
                UpdatedOn = SeedDate
            };
        }

        private static void SeedBooks(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Book>().HasData(
                CreateBook(1, "2020: The Apocalypse", "Li Juan", "6556784356", 15, 1, 13, 5, 10.95M, 25, "/Content/Images/coverimages/apocalypse.png"),
                CreateBook(2, "Children Of Iron", "Nikki Wolf", "7665438976", 16, 1, 11, 6, 13.95M, 3, "/Content/Images/coverimages/childrenofiron.png"),
                CreateBook(3, "Gold In The Dark", "Richard Roe", "5442280765", 17, 1, 13, 5, 6.50M, 10, "/Content/Images/coverimages/goldinthedark.png"),
                CreateBook(4, "Leagues Of Smoke", "Pat Candella", "4556789542", 18, 2, 11, 7, 3M, 1, "/Content/Images/coverimages/leaguesofsmoke.png"),
                CreateBook(5, "Alone With The Stars", "Carlos Salazar", "4563358087", 19, 2, 12, 5, 15.95M, 5, "/Content/Images/coverimages/alonewiththestars.png"),
                CreateBook(6, "The Girl In The Polaroid", "Terri Whitlock", "2354435678", 20, 1, 12, 6, 8.25M, 2, "/Content/Images/coverimages/girlinthepolaroid.png"),
                CreateBook(7, "1001 Jokes", "Mary Major", "6554789632", 21, 2, 11, 5, 13.95M, 7, "/Content/Images/coverimages/1001jokes.png"),
                CreateBook(8, "My Search For Meaning", "Mateo Jackson", "4558786554", 22, 3, 8, 7, 5M, 15, "/Content/Images/coverimages/mysearchformeaning.png")
            );
        }

        private static Book CreateBook(
            int id, string name, string author, string isbn,
            int publisherId, int bookTypeId, int genreId, int conditionId,
            decimal price, int quantity, string coverImageUrl)
        {
            return new Book(name, author, isbn, publisherId, bookTypeId, genreId, conditionId, price, quantity, null, null, coverImageUrl)
            {
                Id = id,
                CreatedOn = SeedDate,
                UpdatedOn = SeedDate
            };
        }
    }
}
