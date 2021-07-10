# ValorantSharp

`ValorantSharp` is an API wrapper to access most of the Valorant services within a single library. It has many features, which include REST calls and an XMPP client built entirely from scratch. It is asynchronous, which means functions of the client can be executed in parallel or in a serial manner when needed and it can easily be used within projects such as Discord Bots, GUIs and more.

## Updates

Most recent version: `0.0.1`

Changes:

- Published the original local repository.
- Modified `ValorantXMPPRegion` mappings.

## Installation

ValorantSharp will be added to `NuGet` in the near future. However, for now, please compile by cloning the repository and building yourself.

## Usage

Basic Version:

```cs
ValorantClient client = new ValorantClientBuilder()
    .WithCredentials("username", "password")
    .WithRegion(ValorantGLZRegion.Region, ValorantXMPPRegion.Region)
    .Build();

// Listen to whatever events you want here.
client.Event += EventHook;

await client.LoginAsync();
```

For a more detailed version with a look at how the commands framework works or how to send messages and presences, please look in the `ValorantSharp.Tests` project.

## Documentation

Coming soon...