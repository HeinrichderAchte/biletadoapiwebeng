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
        public void Controller_HasParameterlessAction_AndReturnsOkWithSeededItem()
        {
            var options = new DbContextOptionsBuilder<AssetsDbContext>()
                .UseInMemoryDatabase(databaseName: "testdb_reservations_get")
                .Options;
            
            using (var seedCtx = new AssetsDbContext(options))
            {
                seedCtx.Set<Reservation>().Add(new Reservation());
                seedCtx.SaveChanges();
            }

            using (var ctx = new AssetsDbContext(options))
            {
                var controller = new ReservationsController(ctx, NullLogger<ReservationsController>.Instance);

                // Suche eine parameterlose öffentliche Action, die IActionResult oder ActionResult<T> zurückgibt
                var method = controller.GetType()
                    .GetMethods(BindingFlags.Public | BindingFlags.Instance)
                    .FirstOrDefault(m => m.GetParameters().Length == 0 &&
                        (typeof(IActionResult).IsAssignableFrom(m.ReturnType) ||
                         (m.ReturnType.IsGenericType && m.ReturnType.GetGenericTypeDefinition() == typeof(ActionResult<>))));

                Assert.NotNull(method);

                var raw = method.Invoke(controller, null);
                Assert.NotNull(raw);

                // Extrahiere IActionResult (unwrapped for ActionResult<T>)
                IActionResult actionResult = raw as IActionResult;
                if (actionResult == null)
                {
                    var resultProp = raw.GetType().GetProperty("Result");
                    if (resultProp != null)
                    {
                        actionResult = resultProp.GetValue(raw) as IActionResult;
                    }
                }

                Assert.NotNull(actionResult);

                // Prüfe OkObjectResult mit einer Auflistung
                var ok = Assert.IsType<OkObjectResult>(actionResult);
                var items = Assert.IsAssignableFrom<IEnumerable<object>>(ok.Value);
                Assert.Single(items);
            }
        }
    }
}
