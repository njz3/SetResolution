using System.Collections.Generic;
using Westwind.SetResolution;
using Xunit;

namespace SetResolution.Tests
{
    public class ModeSelectorTests
    {
        private static DisplaySettings Mode(int width, int height, int frequency, int bits = 32,
            Orientation orientation = Orientation.Default, bool interlaced = false)
        {
            return new DisplaySettings
            {
                Width = width, Height = height, Frequency = frequency, BitCount = bits,
                Orientation = orientation, Interlaced = interlaced
            };
        }

        private static readonly List<DisplaySettings> CrtModes = new List<DisplaySettings>
        {
            Mode(640, 480, 60, 8), Mode(640, 480, 60), Mode(640, 480, 72),
            Mode(1280, 240, 61), Mode(1280, 240, 59),
            Mode(1920, 240, 60),
            Mode(1280, 480, 60, interlaced: true), Mode(1280, 480, 60),
            Mode(1920, 1080, 59), Mode(1920, 1080, 75)
        };

        [Fact]
        public void Find_PrefersSixtyHertzWhenNoFrequencyIsGiven()
        {
            var mode = ModeSelector.Find(CrtModes, new DisplayRequest { Width = 640, Height = 480 }, 72);

            Assert.Equal(60, mode.Frequency);
            Assert.Equal(32, mode.BitCount);
        }

        [Fact]
        public void Find_UsesTheGivenFrequencyOnly()
        {
            Assert.Equal(72, ModeSelector.Find(CrtModes, new DisplayRequest { Width = 640, Height = 480, Frequency = 72 }).Frequency);
            Assert.Null(ModeSelector.Find(CrtModes, new DisplayRequest { Width = 640, Height = 480, Frequency = 50 }));
        }

        [Fact]
        public void Find_WithoutSixtyHertz_KeepsTheCurrentFrequency()
        {
            var mode = ModeSelector.Find(CrtModes, new DisplayRequest { Width = 1920, Height = 1080 }, 75);

            Assert.Equal(75, mode.Frequency);
        }

        [Fact]
        public void Find_WithoutSixtyHertz_TakesTheNearestFrequency()
        {
            Assert.Equal(61, ModeSelector.Find(CrtModes, new DisplayRequest { Width = 1280, Height = 240 }, 60).Frequency);
            Assert.Equal(59, ModeSelector.Find(CrtModes, new DisplayRequest { Width = 1920, Height = 1080 }, 60).Frequency);
        }

        [Fact]
        public void Find_ReturnsNullForAnUnknownSize()
        {
            Assert.Null(ModeSelector.Find(CrtModes, new DisplayRequest { Width = 1024, Height = 384 }));
        }

        [Fact]
        public void Find_RespectsBitCountAndOrientation()
        {
            Assert.Equal(8, ModeSelector.Find(CrtModes, new DisplayRequest { Width = 640, Height = 480, BitCount = 8 }).BitCount);
            Assert.Null(ModeSelector.Find(CrtModes, new DisplayRequest { Width = 640, Height = 480, Orientation = Orientation.Rotate90 }));
        }

        [Fact]
        public void Matches_IgnoresFrequencyWhenNoneIsGiven()
        {
            var request = new DisplayRequest { Width = 640, Height = 480 };

            Assert.True(ModeSelector.Matches(Mode(640, 480, 72), request));
            Assert.False(ModeSelector.Matches(Mode(1920, 1080, 60), request));
            Assert.False(ModeSelector.Matches(null, request));
        }

        [Fact]
        public void Matches_ChecksTheGivenFrequencyAndOrientation()
        {
            var request = new DisplayRequest { Width = 640, Height = 480, Frequency = 60 };

            Assert.True(ModeSelector.Matches(Mode(640, 480, 60), request));
            Assert.False(ModeSelector.Matches(Mode(640, 480, 72), request));
            Assert.False(ModeSelector.Matches(Mode(640, 480, 60, orientation: Orientation.Rotate180), request));
        }

        [Fact]
        public void SameMode_ComparesSizeFrequencyAndOrientation()
        {
            Assert.True(ModeSelector.SameMode(Mode(640, 480, 60), Mode(640, 480, 60, 8)));
            Assert.False(ModeSelector.SameMode(Mode(640, 480, 60), Mode(640, 480, 59)));
            Assert.False(ModeSelector.SameMode(Mode(640, 480, 60), Mode(480, 640, 60)));
            Assert.False(ModeSelector.SameMode(Mode(640, 480, 60), null));
        }

        [Fact]
        public void Describe_GivesTheShortForm()
        {
            Assert.Equal("640x480@60", Mode(640, 480, 60).Describe());
            Assert.Equal("1280x480@60i", Mode(1280, 480, 60, interlaced: true).Describe());
            Assert.Equal("1080x1920@60,Rotate90", Mode(1080, 1920, 60, orientation: Orientation.Rotate90).Describe());
        }
    }
}
