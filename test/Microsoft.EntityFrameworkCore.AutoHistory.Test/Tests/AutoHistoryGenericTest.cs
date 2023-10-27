using System.Collections.Generic;
using System.Linq;
using Microsoft.EntityFrameworkCore.AutoHistoryTests.Contexts;
using Xunit;

namespace Microsoft.EntityFrameworkCore.AutoHistoryTests.Tests;

public class AutoHistoryGenericTest
{
    [Fact]
    public void Entity_Add_AutoHistory_Test()
    {
        using (var db = new GenericBloggingContext())
        {
            db.Blogs.Add(new Blog
            {
                Url = "http://blogs.msdn.com/adonet",
                Posts = new List<Post> {
                    new Post {
                        Title = "xUnit",
                        Content = "Post from xUnit test."
                    }
                },
                PrivateURL = "http://www.secret.com"
            });
            db.EnsureAutoHistory(() => new CustomAutoHistory()
            {
                CustomField = "CustomValue"
            });

            var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Added);


            Assert.Equal(2, count);
        }
    }
    [Fact]
    public void Entity_Update_AutoHistory_Test()
    {
        using (var db = new GenericBloggingContext())
        {
            var blog = new Blog
            {
                Url = "http://blogs.msdn.com/adonet",
                Posts = new List<Post> {
                    new Post {
                        Title = "xUnit",
                        Content = "Post from xUnit test."
                    }
                },
                PrivateURL = "http://www.secret.com"
            };
            db.Attach(blog);
            db.SaveChanges();

            // nullable fix?
            blog.Posts[0].NumViews = 10;
            db.EnsureAutoHistory(() => new CustomAutoHistory()
            {
                CustomField = "CustomValue"
            });
            var count = db.ChangeTracker.Entries().Count(e => e.State == EntityState.Modified);

            Assert.Equal(1, count);
        }
    }
}
