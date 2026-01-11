using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Xunit;
using Xunit.Abstractions;

namespace UnitTests
{
    internal enum OverlapResult
    {
        Free,
        Overlaps,
        InvalidRange
    }

    internal static class ReservationUtils
    {
        public static OverlapResult CheckAvailability(DateOnly newFrom, DateOnly newTo, IEnumerable<(DateOnly From, DateOnly To)> existing)
        {
            if (newFrom > newTo) return OverlapResult.InvalidRange;

            foreach (var r in existing ?? Enumerable.Empty<(DateOnly, DateOnly)>())
            {
                if (newFrom <= r.To && r.From <= newTo)
                {
                    return OverlapResult.Overlaps;
                }
            }

            return OverlapResult.Free;
        }
    }

    public class ReservationErrorTests
    {
        private readonly ITestOutputHelper _output;

        public ReservationErrorTests(ITestOutputHelper output)
        {
            _output = output;
        }

        private void LogScenario(string testName, DateOnly newFrom, DateOnly newTo, IEnumerable<(DateOnly From, DateOnly To)> existing, OverlapResult result)
        {
            _output.WriteLine("========================================");
            _output.WriteLine($"Test: {testName}");
            _output.WriteLine($"NewRange: {newFrom:yyyy-MM-dd} .. {newTo:yyyy-MM-dd}");
            _output.WriteLine("ExistingReservations:");
            foreach (var e in existing ?? Enumerable.Empty<(DateOnly, DateOnly)>())
            {
                _output.WriteLine($"  - {e.From:yyyy-MM-dd} .. {e.To:yyyy-MM-dd}");
            }
            _output.WriteLine($"Result: {result}");
            _output.WriteLine("========================================");
        }

        [Fact]
        public void OverlappingRanges_ReturnsOverlaps()
        {
            var existing = new List<(DateOnly From, DateOnly To)>
            {
                (DateOnly.Parse("2023-01-03"), DateOnly.Parse("2023-01-05"))
            };

            var newFrom = DateOnly.Parse("2023-01-04");
            var newTo = DateOnly.Parse("2023-01-06");

            var result = ReservationUtils.CheckAvailability(newFrom, newTo, existing);

            LogScenario(nameof(OverlappingRanges_ReturnsOverlaps), newFrom, newTo, existing, result);

            result.Should().Be(OverlapResult.Overlaps);
        }

        [Fact]
        public void NonOverlappingRanges_ReturnsFree()
        {
            var existing = new List<(DateOnly From, DateOnly To)>
            {
                (DateOnly.Parse("2023-01-03"), DateOnly.Parse("2023-01-05"))
            };

            var newFrom = DateOnly.Parse("2023-01-06");
            var newTo = DateOnly.Parse("2023-01-08");

            var result = ReservationUtils.CheckAvailability(newFrom, newTo, existing);

            LogScenario(nameof(NonOverlappingRanges_ReturnsFree), newFrom, newTo, existing, result);

            result.Should().Be(OverlapResult.Free);
        }

        [Fact]
        public void TouchingNextDay_ReturnsFree()
        {
            var existing = new List<(DateOnly From, DateOnly To)>
            {
                (DateOnly.Parse("2023-01-01"), DateOnly.Parse("2023-01-05"))
            };

            var newFrom = DateOnly.Parse("2023-01-06");
            var newTo = DateOnly.Parse("2023-01-10");

            var result = ReservationUtils.CheckAvailability(newFrom, newTo, existing);

            LogScenario(nameof(TouchingNextDay_ReturnsFree), newFrom, newTo, existing, result);

            result.Should().Be(OverlapResult.Free);
        }

        [Fact]
        public void InvalidDateRange_ReturnsInvalidRange()
        {
            var existing = new List<(DateOnly From, DateOnly To)>();

            var newFrom = DateOnly.Parse("2023-01-10");
            var newTo = DateOnly.Parse("2023-01-05");

            var result = ReservationUtils.CheckAvailability(newFrom, newTo, existing);

            LogScenario(nameof(InvalidDateRange_ReturnsInvalidRange), newFrom, newTo, existing, result);

            result.Should().Be(OverlapResult.InvalidRange);
        }
    }
}
