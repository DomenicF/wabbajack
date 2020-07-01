﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Compression.BSA;
using Wabbajack.Common;
using Wabbajack.Lib;
using Wabbajack.Lib.CompilationSteps.CompilationErrors;
using Xunit;
using Xunit.Abstractions;
using File = Alphaleonis.Win32.Filesystem.File;
using Path = Alphaleonis.Win32.Filesystem.Path;

namespace Wabbajack.Test
{
    public class MO2SanityTests : ACompilerTest
    {

        public MO2SanityTests(ITestOutputHelper helper) : base(helper)
        {
        }

        
        [Fact]
        public async Task TestDirectMatch() 
        {

            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var testPex = await utils.AddModFile(mod, @"Data\scripts\test.pex", 10);

            await utils.ConfigureMO2();

            await utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", await testPex.ReadAllBytesAsync()}});

            await CompileAndInstallMO2(profile);

            await utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");
        }
        
        [Fact]
        public async Task ExtraFilesInDownloadFolderDontStopCompilation() 
        {

            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var testPex = await utils.AddModFile(mod, @"Data\scripts\test.pex", 10);

            await utils.ConfigureMO2();

            await utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", await testPex.ReadAllBytesAsync()}});
            
            await utils.DownloadsFolder.Combine("some_other_file.7z").WriteAllTextAsync("random data");

            await CompileAndInstallMO2(profile);

            await utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");
        }
        
        [Fact]
        public async Task TestDirectMatchFromGameFolder()
        {

            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var testPex = await utils.AddGameFile(@"enbstuff\test.pex", 10);

            await utils.ConfigureMO2();

            await utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", await testPex.ReadAllBytesAsync()}});

            await CompileAndInstallMO2(profile, useGameFiles: true);

            await utils.VerifyInstalledGameFile(@"enbstuff\test.pex");
        }
        
        [Fact]
        public async Task TestDirectMatchIsIgnoredWhenGameFolderFilesOverrideExists()
        {

            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var testPex = await utils.AddGameFile(@"enbstuff\test.pex", 10);

            await utils.ConfigureMO2();

            utils.SourceFolder.Combine(Consts.GameFolderFilesDir).CreateDirectory();

            await utils.AddManualDownload(
                new Dictionary<string, byte[]> {{"/baz/biz.pex", await testPex.ReadAllBytesAsync()}});

            await CompileAndInstallMO2(profile);

            Assert.False(utils.InstallFolder.Combine(Consts.GameFolderFilesDir, (RelativePath)@"enbstuff\test.pex").IsFile);
        }

        [Fact]
        public async Task TestDuplicateFilesAreCopied()
        {

            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var testPex = await utils.AddModFile(mod, @"Data\scripts\test.pex", 10);

            // Make a copy to make sure it gets picked up and moved around.
            await testPex.CopyToAsync(testPex.WithExtension(new Extension(".copy")));

            await utils.ConfigureMO2();

            await utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/biz.pex", await testPex.ReadAllBytesAsync() } });

            await CompileAndInstallMO2(profile);

            await utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");
            await utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex.copy");
        }

        [Fact]
        public async Task TestUpdating()
        {

            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var unchanged = await utils.AddModFile(mod, @"Data\scripts\unchanged.pex", 10);
            var deleted = await utils.AddModFile(mod, @"Data\scripts\deleted.pex", 10);
            var modified = await utils.AddModFile(mod, @"Data\scripts\modified.pex", 10);

            await utils.ConfigureMO2();

            await utils.AddManualDownload(
                new Dictionary<string, byte[]>
                {
                    { "/baz/unchanged.pex", await unchanged.ReadAllBytesAsync() },
                    { "/baz/deleted.pex", await deleted.ReadAllBytesAsync() },
                    { "/baz/modified.pex", await modified.ReadAllBytesAsync() },
                });

            await CompileAndInstallMO2(profile);

            await utils.VerifyInstalledFile(mod, @"Data\scripts\unchanged.pex");
            await utils.VerifyInstalledFile(mod, @"Data\scripts\deleted.pex");
            await utils.VerifyInstalledFile(mod, @"Data\scripts\modified.pex");

            var unchangedPath = utils.PathOfInstalledFile(mod, @"Data\scripts\unchanged.pex");
            var deletedPath = utils.PathOfInstalledFile(mod, @"Data\scripts\deleted.pex");
            var modifiedPath = utils.PathOfInstalledFile(mod, @"Data\scripts\modified.pex");

            var extraPath = utils.PathOfInstalledFile(mod, @"something_i_made.foo");
            await extraPath.WriteAllTextAsync("bleh");

            var extraFolder = utils.PathOfInstalledFile(mod, @"something_i_made.foo").Parent.Combine("folder_i_made");
            extraFolder.CreateDirectory();
            
            Assert.True(extraFolder.IsDirectory);


            var unchangedModified = unchangedPath.LastModified;

            await modifiedPath.WriteAllTextAsync("random data");
            var modifiedModified = modifiedPath.LastModified;

            await deletedPath.DeleteAsync();

            Assert.True(extraPath.Exists);
            
            await CompileAndInstallMO2(profile);

            await utils.VerifyInstalledFile(mod, @"Data\scripts\unchanged.pex");
            await utils.VerifyInstalledFile(mod, @"Data\scripts\deleted.pex");
            await utils.VerifyInstalledFile(mod, @"Data\scripts\modified.pex");

            Assert.Equal(unchangedModified, unchangedPath.LastModified);
            Assert.NotEqual(modifiedModified, modifiedPath.LastModified);
            Assert.False(extraPath.Exists);
            Assert.False(extraFolder.Exists);
        }

        [Fact]
        public async Task SaveFilesAreIgnored()
        {
            var profile = utils.AddProfile();
            var mod = await utils.AddMod("dummy");

            var saveFolder = utils.SourceFolder.Combine("profiles", profile, "saves");
            saveFolder.CreateDirectory();
            await saveFolder.Combine("incompilation").WriteAllTextAsync("ignore this");

            var installSaveFolderThisProfile = utils.InstallFolder.Combine("profiles", profile, "saves");
            var installSaveFolderOtherProfile = utils.InstallFolder.Combine("profiles", "Other Profile", "saves");
            installSaveFolderThisProfile.CreateDirectory();
            installSaveFolderOtherProfile.CreateDirectory();

            await installSaveFolderOtherProfile.Combine("otherprofile").WriteAllTextAsync("other profile file");
            await installSaveFolderThisProfile.Combine("thisprofile").WriteAllTextAsync("this profile file");

            await utils.ConfigureMO2();
            var modlist = await CompileAndInstallMO2(profile);
            
            Assert.Equal("other profile file", await installSaveFolderOtherProfile.Combine("otherprofile").ReadAllTextAsync());
            Assert.Equal("this profile file", await installSaveFolderThisProfile.Combine("thisprofile").ReadAllTextAsync());
            Assert.False(installSaveFolderThisProfile.Combine("incompilation").Exists);
        }

        [Fact]
        public async Task SetScreenSizeTest()
        {
            var profile = utils.AddProfile();
            var mod = await utils.AddMod("dummy");

            await utils.ConfigureMO2();
            await utils.SourceFolder.Combine("profiles", profile, "somegameprefs.ini").WriteAllLinesAsync(
                // Beth inis are messy, let's make ours just as messy to catch some parse failures
                "[Display]",
                "foo=4",
                "[Display]",
                "STestFile=f",
                "STestFile=",
                "[Display]",
                "foo=4",
                "iSize H=50", 
                "iSize W=100",
                "[MEMORY]",
                "VideoMemorySizeMb=22");

            var modlist = await CompileAndInstallMO2(profile);

            var ini = utils.InstallFolder.Combine("profiles", profile, "somegameprefs.ini").LoadIniFile();

            var sysinfo = CreateDummySystemParameters();

            Assert.Equal(sysinfo.ScreenHeight.ToString(), ini?.Display?["iSize H"]);
            Assert.Equal(sysinfo.ScreenWidth.ToString(), ini?.Display?["iSize W"]);
            Assert.Equal(sysinfo.EnbLEVRAMSize.ToString(), ini?.MEMORY?["VideoMemorySizeMb"]);
        }

        [Fact]
        public async Task UnmodifiedInlinedFilesArePulledFromArchives()
        {
            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var ini = await utils.AddModFile(mod, @"foo.ini", 10);
            await utils.ConfigureMO2();

            await utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/biz.pex", await ini.ReadAllBytesAsync() } });

            var modlist = await CompileAndInstallMO2(profile);
            var directive = modlist.Directives.FirstOrDefault(m => m.To == (RelativePath)$"mods\\{mod}\\foo.ini");

            Assert.NotNull(directive);
            Assert.IsAssignableFrom<FromArchive>(directive);
        }

        [Fact]
        public async Task ModifiedIniFilesArePatchedAgainstFileWithSameName()
        {
            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var ini = await utils.AddModFile(mod, @"foo.ini", 10);
            var meta = await utils.AddModFile(mod, "meta.ini");

            await utils.ConfigureMO2();


            var archive = utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/baz/foo.ini", await ini.ReadAllBytesAsync() } });

            await meta.WriteAllLinesAsync(
                "[General]",
                $"installationFile={archive}");

            // Modify after creating mod archive in the downloads folder
            await ini.WriteAllTextAsync("Wabbajack, Wabbajack, Wabbajack!");

            var modlist = await CompileAndInstallMO2(profile);
            var directive = modlist.Directives.FirstOrDefault(m => m.To == (RelativePath)$"mods\\{mod}\\foo.ini");

            Assert.NotNull(directive);
            Assert.IsAssignableFrom<PatchedFromArchive>(directive);
        }

        [Fact]
        public async Task CanPatchFilesSourcedFromBSAs()
        {
            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var file = await utils.AddModFile(mod, @"baz.bin", 10);
            
            await utils.ConfigureMO2();


            await using var tempFile = new TempFile();
            var bsaState = new BSAStateObject
            {
                Magic = "BSA\0", Version = 0x69, ArchiveFlags = 0x107, FileFlags = 0x0,
            };

            await using (var bsa = bsaState.MakeBuilder(1024 * 1024))
            {
                await bsa.AddFile(new BSAFileStateObject
                {
                    Path = (RelativePath)@"foo\bar\baz.bin", Index = 0, FlipCompression = false
                }, new MemoryStream(utils.RandomData()));
                await bsa.Build(tempFile.Path);
            }
            
            var archive = utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/stuff/files.bsa", await tempFile.Path.ReadAllBytesAsync() } });
            
            await CompileAndInstallMO2(profile);
            await utils.VerifyInstalledFile(mod, @"baz.bin");
            
        }
        
        [Fact]
        public async Task CanNoMatchIncludeFilesFromBSAs()
        {
            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var file = await utils.AddModFile(mod, @"baz.bsa", 10);

            await file.Parent.Combine("meta.ini").WriteAllLinesAsync(new[]
            {
                "[General]", 
                "notes= asdf WABBAJACK_NOMATCH_INCLUDE asdfa"
            });
            
            await utils.ConfigureMO2();


            await using var tempFile = new TempFile();
            var bsaState = new BSAStateObject
            {
                Magic = "BSA\0", Version = 0x69, ArchiveFlags = 0x107, FileFlags = 0x0,
            };


            var tempFileData = utils.RandomData(1024);
            
            await using (var bsa = bsaState.MakeBuilder(1024 * 1024))
            {
                await bsa.AddFile(new BSAFileStateObject
                {
                    Path = (RelativePath)@"matching_file.bin", Index = 0, FlipCompression = false
                }, new MemoryStream(tempFileData));
                await bsa.AddFile(
                    new BSAFileStateObject()
                    {
                        Path = (RelativePath)@"unmatching_file.bin", Index = 1, FlipCompression = false
                    }, new MemoryStream(utils.RandomData(1024)));
                await bsa.Build(file);
            }
            
            var archive = utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/stuff/matching_file_data.bin", tempFileData } });
            
            await CompileAndInstallMO2(profile);
            await utils.VerifyInstalledFile(mod, @"baz.bsa");
            
        }
        
        [Fact]
        public async Task CanInstallFilesFromBSAAndBSA()
        {
            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var file = await utils.AddModFile(mod, @"baz.bin", 128);
            
            await utils.ConfigureMO2();


            await using var tempFile = new TempFile();
            var bsaState = new BSAStateObject
            {
                Magic = "BSA\0", Version = 0x69, ArchiveFlags = 0x107, FileFlags = 0x0,
            };

            await using (var bsa = bsaState.MakeBuilder(1024 * 1024))
            {
                await bsa.AddFile(new BSAFileStateObject
                {
                    Path = (RelativePath)@"foo\bar\baz.bin", Index = 0, FlipCompression = false
                }, new MemoryStream(await file.ReadAllBytesAsync()));
                await bsa.Build(tempFile.Path);
            }
            await tempFile.Path.CopyToAsync(file.Parent.Combine("bsa_data.bsa"));
            
            var archive = utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/stuff/files.bsa", await tempFile.Path.ReadAllBytesAsync() } });
            
            await CompileAndInstallMO2(profile);
            await utils.VerifyInstalledFile(mod, @"baz.bin");
            await utils.VerifyInstalledFile(mod, @"bsa_data.bsa");
            
        }
        
        [Fact]
        public async Task CanRecreateBSAsFromFilesSourcedInOtherBSAs()
        {
            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var file = await utils.AddModFile(mod, @"baz.bsa", 10);
            
            await utils.ConfigureMO2();

            
            var bsaState = new BSAStateObject
            {
                Magic = "BSA\0", Version = 0x69, ArchiveFlags = 0x107, FileFlags = 0x0,
            };

            // Create the download
            await using var tempFile = new TempFile();
            await using (var bsa = bsaState.MakeBuilder(1024 * 1024))
            {
                await bsa.AddFile(new BSAFileStateObject
                {
                    Path = (RelativePath)@"foo\bar\baz.bin", Index = 0, FlipCompression = false
                }, new MemoryStream(utils.RandomData()));
                await bsa.Build(tempFile.Path);
            }
            var archive = utils.AddManualDownload(
                new Dictionary<string, byte[]> { { "/stuff/baz.bsa", await tempFile.Path.ReadAllBytesAsync() } });

            
            // Create the result
            await using (var bsa = bsaState.MakeBuilder(1024 * 1024))
            {
                await bsa.AddFile(new BSAFileStateObject
                {
                    Path = (RelativePath)@"foo\bar\baz.bin", Index = 0, FlipCompression = false
                }, new MemoryStream(utils.RandomData()));
                await bsa.Build(file);
            }

            
            await CompileAndInstallMO2(profile);
            await utils.VerifyInstalledFile(mod, @"baz.bsa");
            
        }

        /* TODO : Disabled For Now
        [Fact]
        public async Task CanSourceFilesFromStockGameFiles()
        {
            Consts.TestMode = false;

            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var skyrimExe = await utils.AddModFile(mod, @"Data\test.exe", 10);

            var gameFolder = Consts.GameFolderFilesDir.RelativeTo(utils.MO2Folder);
            gameFolder.CreateDirectory();

            var gameMeta = Game.SkyrimSpecialEdition.MetaData();
            await gameMeta.GameLocation().Combine(gameMeta.MainExecutable!).CopyToAsync(skyrimExe);
            await gameMeta.GameLocation().Combine(gameMeta.MainExecutable!).CopyToAsync(gameFolder.Combine(gameMeta.MainExecutable!));
            
            await utils.Configure();
            
            await CompileAndInstall(profile);

            utils.VerifyInstalledFile(mod, @"Data\test.exe");

            Assert.False("SkyrimSE.exe".RelativeTo(utils.DownloadsFolder).Exists, "File should not appear in the download folder because it should be copied from the game folder");

            var file = "ModOrganizer.ini".RelativeTo(utils.InstallFolder);
            Assert.True(file.Exists);

            var ini = file.LoadIniFile();
            Assert.Equal(((AbsolutePath)(string)ini?.General?.gamePath).Combine(gameMeta.MainExecutable), 
                Consts.GameFolderFilesDir.Combine(gameMeta.MainExecutable).RelativeTo(utils.InstallFolder));
            
            Consts.TestMode = true;
        }
        */
        
        [Fact]
        public async Task NoMatchIncludeIncludesNonMatchingFiles() 
        {

            var profile = utils.AddProfile();
            var mod = await utils.AddMod();
            var testPex = await utils.AddModFile(mod, @"Data\scripts\test.pex", 10);

            await utils.ConfigureMO2();

            await (await utils.AddModFile(mod, "meta.ini")).WriteAllLinesAsync(new[]
            {
                "[General]", "notes= fsdaf WABBAJACK_NOMATCH_INCLUDE fadsfsad",
            });
            await CompileAndInstallMO2(profile);

            await utils.VerifyInstalledFile(mod, @"Data\scripts\test.pex");
        }
        
        [Fact]
        public async Task CanSourceFilesFromTheGameFiles() 
        {

            var profile = utils.AddProfile();
            var mod = await utils.AddMod();

            Game.SkyrimSpecialEdition.MetaData().CanSourceFrom = new[] {Game.Morrowind, Game.Skyrim};
            
            // Morrowind file with different name
            var mwFile = Game.Morrowind.MetaData().GameLocation().Combine("Data Files", "Bloodmoon.esm");
            var testMW = await utils.AddModFile(mod, @"Data\MW\Bm.esm");
            await mwFile.CopyToAsync(testMW);

            // SkyrimSE file with same name
            var skyrimFile = Game.SkyrimSpecialEdition.MetaData().GameLocation().Combine("Data", "Update.esm");
            var testSky = await utils.AddModFile(mod, @"Data\SkyrimSE\Update.esm.old");
            await skyrimFile.CopyToAsync(testSky);

            // Same game, but patched ata
            
            var pdata = utils.RandomData(1024);
            var testSkySE = await utils.AddModFile(mod, @"Data\SkyrimSE\Update.esm");
            await testSkySE.WriteAllBytesAsync(pdata);
            

            await utils.ConfigureMO2();

            await CompileAndInstallMO2(profile, useGameFiles: true);

            await utils.VerifyInstalledFile(mod, @"Data\MW\Bm.esm");
            await utils.VerifyInstalledFile(mod, @"Data\SkyrimSE\Update.esm.old");
            await utils.VerifyInstalledFile(mod, @"Data\SkyrimSE\Update.esm");
            
        }
        
        
        /// <summary>
        /// Issue #861 : https://github.com/wabbajack-tools/wabbajack/issues/861
        /// </summary>
        /// <returns></returns>
        [Fact]
        public async Task AlwaysEnabledModsRetainTheirOrder() 
        {

            var profile = utils.AddProfile();
            var enabledMod = await utils.AddMod();
            var enabledTestPex = await utils.AddModFile(enabledMod, @"Data\scripts\enabledTestPex.pex", 10);

            var disabledMod = await utils.AddMod();
            var disabledTestPex = await utils.AddModFile(disabledMod, @"Data\scripts\disabledTestPex.pex", 10);

            await disabledMod.RelativeTo(utils.ModsFolder).Combine("meta.ini").WriteAllLinesAsync(
                "[General]",
                $"notes={Consts.WABBAJACK_ALWAYS_ENABLE}");

            await utils.ConfigureMO2(new []
            {
                (disabledMod, false),
                (enabledMod, true)
            });

            await utils.AddManualDownload(
                new Dictionary<string, byte[]>
                {
                    {"/file1.pex", await enabledTestPex.ReadAllBytesAsync()},
                    {"/file2.pex", await disabledTestPex.ReadAllBytesAsync()},
                });

            await CompileAndInstallMO2(profile);

            await utils.VerifyInstalledFile(enabledMod, @"Data\scripts\enabledTestPex.pex");
            await utils.VerifyInstalledFile(disabledMod, @"Data\scripts\disabledTestPex.pex");

            var modlistTxt = await utils.InstallFolder.Combine("profiles", profile, "modlist.txt").ReadAllLinesAsync();
            Assert.Equal(new string[]
            {
                $"-{disabledMod}",
                $"+{enabledMod}"
            }, modlistTxt.ToArray());
        }

    }
}