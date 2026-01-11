using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Serilog;
using Xunit;
using Biletado.Services;
using Biletado.Controller;
using Biletado.Persistence.Contexts; 
using Biletado.Repository; 
using Biletado.DTOs.Response;
using Biletado.DTOs.Request;

namespace Biletado.Api.Reservations.UnitTests
{
    public class ReservationErrorTests
    {
        [Fact]
        public async Task InvalidDateRange_ThrowsArgumentException()
        {
            var roomId = Guid.NewGuid();
            var newFrom = DateOnly.Parse("2023-01-05");
            var newTo = DateOnly.Parse("2023-01-01"); // newFrom > newTo

            var mockRepo = new Mock<IReservationRepository>();
            mockRepo.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Reservation>());

            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                      .Returns(mockLogger.Object);

            var service = new ReservationService(
                mockRepo.Object,
                mockLogger.Object,
                null!,
                null!
            );

            Func<Task> act = async () => await service.IsRoomFree(roomId, newFrom, newTo, CancellationToken.None);
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task EmptyRoomId_ThrowsArgumentException()
        {
            var roomId = Guid.Empty;
            var newFrom = DateOnly.Parse("2023-01-01");
            var newTo = DateOnly.Parse("2023-01-05");

            var mockRepo = new Mock<IReservationRepository>();
            mockRepo.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                    .ReturnsAsync(new List<Reservation>());

            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                      .Returns(mockLogger.Object);

            var service = new ReservationService(
                mockRepo.Object,
                mockLogger.Object,
                null!,
                null!
            );

            Func<Task> act = async () => await service.IsRoomFree(roomId, newFrom, newTo, CancellationToken.None);
            await act.Should().ThrowAsync<ArgumentException>();
        }

        [Fact]
        public async Task RepositoryException_IsPropagated()
        {
            var roomId = Guid.NewGuid();
            var newFrom = DateOnly.Parse("2023-01-01");
            var newTo = DateOnly.Parse("2023-01-05");

            var mockRepo = new Mock<IReservationRepository>();
            mockRepo.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new InvalidOperationException("Repository failure"));

            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                      .Returns(mockLogger.Object);

            var service = new ReservationService(
                mockRepo.Object,
                mockLogger.Object,
                null!,
                null!
            );

            Func<Task> act = async () => await service.IsRoomFree(roomId, newFrom, newTo, CancellationToken.None);
            var ex = await act.Should().ThrowAsync<InvalidOperationException>();
            ex.WithMessage("Repository failure");
        }

        [Fact]
        public async Task Cancellation_ThrowsOperationCanceledException()
        {
            var roomId = Guid.NewGuid();
            var newFrom = DateOnly.Parse("2023-01-01");
            var newTo = DateOnly.Parse("2023-01-05");

            var mockRepo = new Mock<IReservationRepository>();
            mockRepo.Setup(r => r.GetAllAsync(false, It.IsAny<CancellationToken>()))
                    .ThrowsAsync(new OperationCanceledException());

            var mockLogger = new Mock<ILogger>();
            mockLogger.Setup(l => l.ForContext(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<bool>()))
                      .Returns(mockLogger.Object);

            var service = new ReservationService(
                mockRepo.Object,
                mockLogger.Object,
                null!,
                null!
            );

            Func<Task> act = async () => await service.IsRoomFree(roomId, newFrom, newTo, CancellationToken.None);
            await act.Should().ThrowAsync<OperationCanceledException>();
        }
    }
}
