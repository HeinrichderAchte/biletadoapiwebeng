using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;
using Biletado.Controller;
using Biletado.Models;

namespace UnitTests
{
    public class ReservationsControllerTests
    {
        [Fact]
        public void Get_ReturnsOkWithReservations()
        {
            var options = new DbContextOptionsBuilder<AssetsDbContext>()
                .UseInMemoryDatabase(databaseName: "testdb_reservations_get")
                .Options;

            // Seed data
            using (var seedCtx = new AssetsDbContext(options))
            {
                // Anpassung: ersetze Eigenschaften mit denen deiner Reservation-Entität
                seedCtx.Set<Reservation>().Add(new Reservation { Id = 1, CustomerName = "Test Customer" });
                seedCtx.SaveChanges();
            }

            // Act
            using (var ctx = new AssetsDbContext(options))
            {
                var controller = new ReservationsController(ctx, NullLogger<ReservationsController>.Instance);

                // Anpassung: ersetze 'Get' durch den tatsächlichen Action-Namen falls nötig
                var raw = controller.Get();

                if (raw is OkObjectResult ok)
                {
                    var items = Assert.IsAssignableFrom<IEnumerable<Reservation>>(ok.Value);
                    Assert.Single(items);
                }
                else
                {
                    // Falls die Action ein ActionResult<T> zurückgibt
                    var action = raw as ActionResult<IEnumerable<Reservation>>;
                    Assert.NotNull(action);
                    var okResult = Assert.IsType<OkObjectResult>(action.Result);
                    var items = Assert.IsAssignableFrom<IEnumerable<Reservation>>(okResult.Value);
                    Assert.Single(items);
                }
            }
        }
    }
}
