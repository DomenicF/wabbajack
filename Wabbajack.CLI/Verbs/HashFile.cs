﻿using System;
using System.Threading.Tasks;
using CommandLine;
using Wabbajack.Common;

namespace Wabbajack.CLI.Verbs
{
    [Verb("hash-file", HelpText = "Hash a file and print the result")]
    public class HashFile : AVerb
    {

        [Option('i', "input", Required = true, HelpText = "Input file name")]
        public string Input { get; set; } = "";

        protected override async Task<ExitCode> Run()
        {
            var abs = (AbsolutePath)Input;
            var hash = await abs.FileHashAsync();
            Console.WriteLine($"{abs} hash: {hash} {hash.ToHex()} {(long)hash}");
            return ExitCode.Ok;
        }
    }
}
