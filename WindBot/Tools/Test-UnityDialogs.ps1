#Requires -Version 7.0
# Compile the production Unity branch with PowerShell's JSON.NET and no framework JSON serializer.
[CmdletBinding()]
param()

$ErrorActionPreference = 'Stop'
$windbot = Split-Path -Parent $PSScriptRoot
$source = Get-Content -LiteralPath (Join-Path $windbot 'Game/AI/Dialogs.cs') -Raw

# The reader only needs the file service, random source and chat endpoint from a game session.
$hostTypes = @'
namespace WindBot
{
    public static class Program
    {
        public static string Root;
        public static readonly System.Random Rand = new System.Random(0);

        public static System.IO.FileStream ReadFile(string directory, string name, string extension)
        {
            return System.IO.File.OpenRead(System.IO.Path.Combine(Root, directory, name + "." + extension));
        }
    }
}

namespace WindBot.Game
{
    public class GameClient
    {
        public string Dialog;
        public bool _chat = true;
        public string Message;
        public void Chat(string message) { Message = message; }
    }
}
'@

$references = @([Newtonsoft.Json.JsonConvert].Assembly.Location, 'System.Collections.dll',
                'System.Runtime.Serialization.Primitives.dll')
Add-Type -TypeDefinition ($source + [Environment]::NewLine + $hostTypes) -ReferencedAssemblies $references `
    -CompilerOptions '/define:UNITY_5_3_OR_NEWER', '/nowarn:1701'
[WindBot.Program]::Root = $windbot
$files = @(Get-ChildItem -LiteralPath (Join-Path $windbot 'Dialogs') -Filter '*.json' -File)
if ('default.json' -cnotin $files.Name) { throw 'The default WindBot dialog is missing.' }
foreach ($file in $files) {
    $client = [WindBot.Game.GameClient]::new()
    $client.Dialog = $file.BaseName
    try {
        $dialogs = [WindBot.Game.AI.Dialogs]::new($client)
        if ($file.Name -ceq 'default.json') {
            $dialogs.SendWelcome()
            if ([string]::IsNullOrWhiteSpace($client.Message)) { throw 'Expected a welcome message.' }
        }
    } catch {
        throw "Failed to load $($file.Name): $($_.Exception.GetBaseException().Message)"
    }
}
Write-Output "The Unity dialog reader loaded $($files.Count) files."
