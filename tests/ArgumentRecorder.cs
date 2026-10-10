// Copyright (c) 2026 Doug Lawlor.
// SPDX-License-Identifier: MIT
// See LICENSE for the full license text.
using System;
using System.IO;

internal static class ArgumentRecorder
{
    private static void Main(string[] args)
    {
        File.WriteAllLines(Environment.GetEnvironmentVariable("RNOTEPAD_TEST_OUTPUT"), Array.ConvertAll(args, x => Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes(x))));
    }
}
