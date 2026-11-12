using System.IO;
using Westwind.SetResolution;
using Xunit;

namespace SetResolution.Tests
{
    public class CommandLineTests
    {
        // The executable path contains "-w" and "-m" on purpose: switches must not be read there.
        private const string Exe = @"C:\setresolution-0.4.0-njz3-windows\my-monitor\SetResolution.exe";

        private static SetResolutionCommandLineParser Parse(params string[] arguments)
        {
            var args = new string[arguments.Length + 1];
            args[0] = Exe;
            arguments.CopyTo(args, 1);

            var parser = new SetResolutionCommandLineParser(args, Exe + " " + string.Join(" ", arguments));
            parser.Parse();
            return parser;
        }

        [Fact]
        public void ParsesTheCabinetScriptLine()
        {
            var parser = Parse("-w", "640", "-h", "480", "-noprompt");

            Assert.Equal("-w", parser.FirstParameter);
            Assert.Equal(640, parser.Width);
            Assert.Equal(480, parser.Height);
            Assert.True(parser.NoPrompt);
            Assert.False(parser.NoPersist);
            Assert.False(parser.FrequencyGiven);
            Assert.Equal(60, parser.Frequency);
            Assert.Equal(32, parser.BitCount);
            Assert.Equal(-1, parser.MonitorId);
            Assert.Equal(RacecabWitness.DefaultFolder, parser.WitnessFolder);
        }

        [Fact]
        public void ParsesRestoreWithFrequencyMonitorAndTemp()
        {
            var parser = Parse("RESTORE", "-w", "1280", "-h", "240", "-f", "61", "-m2", "-temp");

            Assert.Equal("RESTORE", parser.FirstParameter);
            Assert.Equal(1280, parser.Width);
            Assert.Equal(240, parser.Height);
            Assert.True(parser.FrequencyGiven);
            Assert.Equal(61, parser.Frequency);
            Assert.Equal(2, parser.MonitorId);
            Assert.True(parser.NoPersist);
            Assert.False(parser.NoPrompt);
        }

        [Fact]
        public void ParsesNoPersistQuotedProfileAndFolder()
        {
            var parser = Parse("SET", "-p", "My Profile", "-nopersist", "-dir", @"D:\Image Racecab");

            Assert.Equal("My Profile", parser.Profile);
            Assert.True(parser.NoPersist);
            Assert.Equal(@"D:\Image Racecab", parser.WitnessFolder);
            Assert.Equal(0, parser.Width);
        }

        [Fact]
        public void ParsesAttachedValues()
        {
            var parser = Parse("SET", "-w1920", "-h1080", "-pFull");

            Assert.Equal(1920, parser.Width);
            Assert.Equal(1080, parser.Height);
            Assert.Equal("Full", parser.Profile);
        }

        [Fact]
        public void IgnoresTheExecutablePath()
        {
            var parser = Parse("CURRENT");

            Assert.Equal(0, parser.Width);
            Assert.Equal(-1, parser.MonitorId);
            Assert.False(parser.NoPrompt);
        }

        [Fact]
        public void ProfileFrequencyCountsAsGiven()
        {
            var parser = Parse("SET");
            new DisplayProfile { Name = "x", Width = 640, Height = 480, Frequency = 60 }.UpdateCommandLine(parser);

            Assert.True(parser.FrequencyGiven);
        }
    }

    public class RacecabWitnessTests
    {
        [Theory]
        [InlineData("Configured_for_640x480.txt", 640, 480)]
        [InlineData("Configured_for_1280x240.txt", 1280, 240)]
        [InlineData("configured_for_1024x384.TXT", 1024, 384)]
        [InlineData("Configured_for3840x2160.txt", 3840, 2160)]
        public void ReadsTheResolutionFromTheFileName(string name, int width, int height)
        {
            var request = RacecabWitness.FromFileNames(new[] { @"C:\Racecab\" + name }, @"C:\Racecab", out string file, out string error);

            Assert.Null(error);
            Assert.Equal(@"C:\Racecab\" + name, file);
            Assert.Equal(width, request.Width);
            Assert.Equal(height, request.Height);
            Assert.Equal(0, request.Frequency);
        }

        [Fact]
        public void FailsWithoutWitnessFile()
        {
            var request = RacecabWitness.FromFileNames(new[] { @"C:\Racecab\Configured_for_SSD.txt", @"C:\Racecab\readme.txt" },
                @"C:\Racecab", out _, out string error);

            Assert.Null(request);
            Assert.Contains("no witness file", error);
        }

        [Fact]
        public void FailsWhenWitnessFilesDisagree()
        {
            var request = RacecabWitness.FromFileNames(new[] { @"C:\Racecab\Configured_for_640x480.txt", @"C:\Racecab\Configured_for_1920x1080.txt" },
                @"C:\Racecab", out _, out string error);

            Assert.Null(request);
            Assert.Contains("640x480, 1920x1080", error);
        }

        [Fact]
        public void AcceptsTwoFilesWithTheSameResolution()
        {
            var request = RacecabWitness.FromFileNames(new[] { @"C:\Racecab\Configured_for_640x480.txt", @"C:\Racecab\Configured_for640x480.txt" },
                @"C:\Racecab", out _, out string error);

            Assert.Null(error);
            Assert.Equal(640, request.Width);
        }

        [Fact]
        public void ReadsAFolder()
        {
            string folder = Path.Combine(Path.GetTempPath(), "SetResolution.Tests." + Path.GetRandomFileName());
            Directory.CreateDirectory(folder);
            try
            {
                File.WriteAllText(Path.Combine(folder, "Configured_for_1920x240.txt"), "Configuration done for SSD_1920x240");

                var request = RacecabWitness.FromFolder(folder, out string file, out string error);

                Assert.Null(error);
                Assert.Equal(Path.Combine(folder, "Configured_for_1920x240.txt"), file);
                Assert.Equal(1920, request.Width);
                Assert.Equal(240, request.Height);
                Assert.Null(RacecabWitness.FromFolder(Path.Combine(folder, "missing"), out _, out error));
                Assert.Contains("does not exist", error);
            }
            finally
            {
                Directory.Delete(folder, true);
            }
        }
    }

    public class ExitCodeTests
    {
        [Fact]
        public void MapsDisplayChangeResults()
        {
            Assert.Equal(0, ExitCodes.FromDisplayChange(DisplayChangeResult.Successful));
            Assert.Equal(10, ExitCodes.FromDisplayChange(DisplayChangeResult.Restart));
            Assert.Equal(11, ExitCodes.FromDisplayChange(DisplayChangeResult.Failed));
            Assert.Equal(12, ExitCodes.FromDisplayChange(DisplayChangeResult.BadMode));
            Assert.Equal(13, ExitCodes.FromDisplayChange(DisplayChangeResult.NotUpdated));
            Assert.Equal(14, ExitCodes.FromDisplayChange(DisplayChangeResult.BadFlags));
            Assert.Equal(15, ExitCodes.FromDisplayChange(DisplayChangeResult.BadParam));
            Assert.Equal(16, ExitCodes.FromDisplayChange(DisplayChangeResult.BadDualView));
        }
    }
}
