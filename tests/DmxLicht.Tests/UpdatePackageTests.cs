using System.Runtime.InteropServices;
using DmxLicht.Core;

namespace DmxLicht.Tests;

/// <summary>The pure part of updating: which release file to take, reading .sha256 files, the sudo call on a Pi.</summary>
public class UpdatePackageTests
{
    static readonly (string Name, string Url)[] Release2144 =
    {
        ("DMX-Light-Control-2.1.44.zip", "u/win.zip"),
        ("DMX-Light-Control-2.1.44.zip.sha256", "u/win.zip.sha256"),
        ("DMX-Light-Control-2.1.44-linux-arm64.tar.gz", "u/pi.tgz"),
        ("DMX-Light-Control-2.1.44-linux-arm64.tar.gz.sha256", "u/pi.tgz.sha256"),
        ("DmxLicht-2.1.44.zip", "u/old.zip"),
        ("DmxLicht-2.1.44.zip.sha256", "u/old.zip.sha256"),
    };

    [Fact]
    public void Suffix_per_platform()
    {
        Assert.Equal(".zip", UpdatePackage.Suffix(true, Architecture.X64));
        Assert.Equal("-linux-arm64.tar.gz", UpdatePackage.Suffix(false, Architecture.Arm64));
        Assert.Equal("-linux-x64.tar.gz", UpdatePackage.Suffix(false, Architecture.X64));
    }

    [Fact]
    public void Pick_takes_the_package_for_this_computer()
    {
        Assert.Equal(("u/win.zip", "u/win.zip.sha256"), UpdatePackage.Pick(Release2144, "2.1.44", ".zip"));
        Assert.Equal(("u/pi.tgz", "u/pi.tgz.sha256"), UpdatePackage.Pick(Release2144, "2.1.44", "-linux-arm64.tar.gz"));
    }

    [Fact]
    public void Pick_needs_both_package_and_checksum()
    {
        // a Pi package without its .sha256 is never installed; and there is no x64 package at all
        var noSha = Release2144.Where(a => a.Name != "DMX-Light-Control-2.1.44-linux-arm64.tar.gz.sha256");
        Assert.Equal((null, null), UpdatePackage.Pick(noSha, "2.1.44", "-linux-arm64.tar.gz"));
        Assert.Equal((null, null), UpdatePackage.Pick(Release2144, "2.1.44", "-linux-x64.tar.gz"));
        Assert.Equal((null, null), UpdatePackage.Pick(Release2144, "2.1.45", ".zip"));
    }

    [Fact]
    public void Pick_falls_back_to_the_old_name_and_ignores_case()
    {
        var oldOnly = new[] { ("dmxlicht-2.1.10.ZIP", "u/a"), ("DmxLicht-2.1.10.zip.sha256", "u/b") };
        Assert.Equal(("u/a", "u/b"), UpdatePackage.Pick(oldOnly, "2.1.10", ".zip"));
    }

    [Fact]
    public void FileName_always_uses_the_current_name()
    {
        Assert.Equal("DMX-Light-Control-2.1.44-linux-arm64.tar.gz", UpdatePackage.FileName("2.1.44", "-linux-arm64.tar.gz"));
        Assert.Equal("DMX-Light-Control-2.1.44.zip", UpdatePackage.FileName("2.1.44", ".zip"));
    }

    [Theory]
    [InlineData("ABCDEF0123456789abcdef0123456789ABCDEF0123456789abcdef0123456789  DMX-Light-Control-2.1.44.zip\n")]
    [InlineData("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789")]
    [InlineData("  abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789 *DMX-Light-Control-2.1.44.zip\r\n")]
    [InlineData("*abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789")]
    public void ParseSha256_reads_sha256sum_files(string text) =>
        Assert.Equal("abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789", UpdatePackage.ParseSha256(text));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("Not Found")]
    [InlineData("abcdef0123456789")]                                                            // too short
    [InlineData("zzcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789")]           // not hex
    [InlineData("<html>abcdef0123456789abcdef0123456789abcdef0123456789abcdef0123456789</html>")]
    public void ParseSha256_rejects_what_is_not_a_hash(string text) => Assert.Null(UpdatePackage.ParseSha256(text));

    [Theory]
    [InlineData("2.1.44", true)]
    [InlineData("10.0.123456", true)]
    [InlineData("v2.1.44", false)]
    [InlineData("2.1", false)]
    [InlineData("2.1.44-beta", false)]
    [InlineData("2.1.44 /tmp/x", false)]
    [InlineData("2.1.44;reboot", false)]
    [InlineData("--apply", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsPlainVersion_only_bare_numbers(string v, bool ok) => Assert.Equal(ok, UpdatePackage.IsPlainVersion(v));

    [Fact]
    public void HelperArgs_are_separate_arguments_for_sudo()
    {
        var args = UpdatePackage.HelperArgs("2.1.44", "/var/lib/dmx-light-control/Updates/DMX-Light-Control-2.1.44-linux-arm64.tar.gz");
        Assert.Equal(new[]
        {
            "-n",                                                  // never wait for a password
            "/usr/local/lib/dmx-light-control/update.sh",
            "2.1.44",
            "/var/lib/dmx-light-control/Updates/DMX-Light-Control-2.1.44-linux-arm64.tar.gz",
        }, args);
    }

    [Theory]
    [InlineData("--apply", "/var/lib/dmx-light-control/Updates/x.tar.gz")]
    [InlineData("2.1.44 x", "/var/lib/dmx-light-control/Updates/x.tar.gz")]
    [InlineData("2.1.44", "Updates/x.tar.gz")]                                             // relative
    [InlineData("2.1.44", "")]
    [InlineData("2.1.44", null)]
    public void HelperArgs_refuse_odd_input(string version, string package) =>
        Assert.Throws<ArgumentException>(() => UpdatePackage.HelperArgs(version, package));

    [Fact]
    public void ParseResult_reads_what_the_helper_leaves()
    {
        Assert.Equal((true, "2.1.44", (string)null), UpdatePackage.ParseResult("ok 2.1.44\n"));
        Assert.Equal((false, "2.1.44", "de nieuwe versie blijft niet draaien"),
            UpdatePackage.ParseResult("failed 2.1.44: de nieuwe versie blijft niet draaien\n"));
        Assert.Null(UpdatePackage.ParseResult(""));
        Assert.Null(UpdatePackage.ParseResult("something else"));
    }
}
