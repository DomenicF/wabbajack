﻿using System;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Threading.Tasks;
using Wabbajack.Common;
using Wabbajack.Lib.Downloaders;
using Wabbajack.Lib.NexusApi;
using Xunit;
using Xunit.Abstractions;

namespace Wabbajack.VirtualFileSystem.Test
{
    public class FileExtractorTests : IAsyncLifetime
    {
        private ITestOutputHelper _helper;
        private IDisposable _unsub;

        public FileExtractorTests(ITestOutputHelper helper)
        {
            _helper = helper;
            _unsub = Utils.LogMessages.Subscribe(f =>
            {
                try
                {
                    _helper.WriteLine(f.ShortDescription);
                }
                catch (Exception _)
                {
                    // ignored
                }
            });
        }

        public async Task InitializeAsync()
        {
        }

        public async Task DisposeAsync()
        {
            _unsub.Dispose();
        }
        
        [Fact]
        public async Task CanGatherDataFromZipFiles()
        {
            await using var temp = await TempFolder.Create();
            await using var archive = new TempFile();
            for (int i = 0; i < 10; i ++)
            {
                await WriteRandomData(temp.Dir.Combine($"{i}.bin"), _rng.Next(10, 1024));
            }

            await ZipUpFolder(temp.Dir, archive.Path, false);
            
            var results = await FileExtractor2.GatheringExtract(new NativeFileStreamFactory(archive.Path), 
                _ => true,
                async (path, sfn) =>
                {
                    await using var s = await sfn.GetStream();
                    return await s.xxHashAsync();
                });
            
            Assert.Equal(10, results.Count);
            foreach (var (path, hash) in results)
            {
                Assert.Equal(await temp.Dir.Combine(path).FileHashAsync(), hash);
            }
        }
        
        [Fact]
        public async Task SmallFilesShouldntCrash()
        {
            await using var temp = await TempFolder.Create();
            await using var archive = new TempFile();
            for (int i = 0; i < 1; i ++)
            {
                await WriteRandomData(temp.Dir.Combine($"{i}.bin"), _rng.Next(10, 10));
            }

            await ZipUpFolder(temp.Dir, archive.Path, false);

            var results = await FileExtractor2.GatheringExtract(new NativeFileStreamFactory(archive.Path), 
                _ => true,
                async (path, sfn) =>
                {
                    await using var s = await sfn.GetStream();
                    return await s.xxHashAsync();
                });
            
            Assert.Equal(1, results.Count);
            foreach (var (path, hash) in results)
            {
                Assert.Equal(await temp.Dir.Combine(path).FileHashAsync(), hash);
            }
        }


        [Fact]
        public async Task CanExtractEmptyFiles()
        {
            await using var temp = await TempFolder.Create();
            await using var archive = new TempFile();
            
            for (int i = 0; i < 1; i ++)
            {
                await WriteRandomData(temp.Dir.Combine($"{i}.bin"), _rng.Next(10, 1024));
            }
            await (await temp.Dir.Combine("empty.txt").Create()).DisposeAsync();

            await ZipUpFolder(temp.Dir, archive.Path, false);
            
            var results = await FileExtractor2.GatheringExtract(new NativeFileStreamFactory(archive.Path), 
                _ => true,
                async (path, sfn) =>
                {
                    await using var s = await sfn.GetStream();
                    return await s.xxHashAsync();
                });
            
            Assert.Equal(2, results.Count);
            foreach (var (path, hash) in results)
            {
                Assert.Equal(await temp.Dir.Combine(path).FileHashAsync(), hash);
            }
        }
        
        private static Extension OMODExtension = new Extension(".omod");
        private static Extension CRCExtension = new Extension(".crc");

        [Fact]
        public async Task CanGatherDataFromOMODFiles()
        {
            var src = await DownloadMod(Game.Oblivion, 18498);

            await FileExtractor2.GatheringExtract(new NativeFileStreamFactory(src),
                p => p.Extension == OMODExtension, async (path, sfn) =>
                {
                    await FileExtractor2.GatheringExtract(sfn, _ => true, async (ipath, isfn) => {
                        // We shouldn't have any .crc files because this file should be recognized as a OMOD and extracted correctly
                        Assert.NotEqual(CRCExtension, ipath.Extension);
                        return 0;
                    });
                    return 0;
                });
        }

        [Fact]
        public async Task SmallZipNoLongerCrashes()
        {
            var src = await DownloadMod(Game.Fallout4, 29596, 120918);
            await using var tmpFolder = await TempFolder.Create();
            await FileExtractor2.ExtractAll(src, tmpFolder.Dir);
        }




        private static readonly Random _rng = new Random();
        private static async Task WriteRandomData(AbsolutePath path, int size)
        {
            var buff = new byte[size];
            _rng.NextBytes(buff);
            await path.WriteAllBytesAsync(buff);
        }
        
        
        private async Task WriteRandomData(Stream path, long size)
        {
            var buff = new byte[size];
            _rng.NextBytes(buff);
            await path.WriteAsync(buff);
        }
        
        private static async Task AddFile(AbsolutePath filename, string text)
        {
            filename.Parent.CreateDirectory();
            await filename.WriteAllTextAsync(text);
        }

        private static async Task ZipUpFolder(AbsolutePath folder, AbsolutePath output, bool deleteSource = true)
        {
            ZipFile.CreateFromDirectory((string)folder, (string)output);
            if (deleteSource) 
                await folder.DeleteDirectory();
        }
        
        
        private static AbsolutePath _stagingFolder = ((RelativePath)"NexusDownloads").RelativeToEntryPoint();

        private static async Task<AbsolutePath> DownloadMod(Game game, int mod)
        {
            using var client = await NexusApiClient.Get();
            var results = await client.GetModFiles(game, mod);
            var file = results.files.FirstOrDefault(f => f.is_primary) ??
                       results.files.OrderByDescending(f => f.uploaded_timestamp).First();
            return await DownloadNexusFile(game, mod, file);
        }

        private static async Task<AbsolutePath> DownloadNexusFile(Game game, int mod, NexusFileInfo file)
        {
            var src = _stagingFolder.Combine(file.file_name);

            if (src.Exists) return src;

            var state = new NexusDownloader.State
            {
                ModID = mod,
                Game = game,
                FileID = file.file_id
            };
            await state.Download(src);
            return src;
        }

        private async Task<AbsolutePath> DownloadMod(Game game, int mod, int fileId)
        {
            using var client = await NexusApiClient.Get();
            var results = await client.GetModFiles(game, mod);
            var file = results.files.FirstOrDefault(f => f.file_id == fileId);
            return await DownloadNexusFile(game, mod, file);

        }
        
    }
}
