# CONTRIBUTION

Welcome ro RavenM's contributing guide, thanks for your support!

The doc itself is still in an early stage, so fell free to ask us on [Discord Server](https://discord.gg/63zE4gY)!

## Contribution

For normal players, you would better to comment at [docs](https://ravenfieldcommunity.github.io/docs/en/Projects/ravenm.html) or raise post on our dicsord server, that is enough for us to improve RavenM and its user guides.

We recommend you to learn about github on [github docs](https://docs.github.com/en/get-started/start-your-journey/about-github-and-git) first and bepinex plugin development on [bepinex docs](https://docs.bepinex.dev/), to get used to the workflow of open source project contribution.

## Plugin architecture

### Overview

As we all know, RavenM is base on BepInEX, it help RavenM inject into original game to run its own code and edit game.

The plugin is fromed of these parts: 
  - harmony patches
  
      Edit original game logic to make it suitable for multiplayer, and get something in game-internal to use.
  - function(feature) components
    
      Provide some extra features like game config sync, chat and data packets sending.
      
  - outside dependencies
  
      Provide RavenM with something useful, like `SimpleJSON` to parse and solve json files, `Steamworks` to communicate with steam open api
      
The full lifetime architecture:

  - BepInEX inject, RavenM startup =>
  - RavenM self-init, patch game and init feature components =>
  - In game stage, feature components and patch instances runs with original game, and do their jobs.
  

## Build

[.NET SDK](https://dotnet.microsoft.com/en-us/download) is required, and need 4.6 or newer.

Tested on sdk 9.0 and 8.0.

To build it or set up the local git repo, clone the repository to your local machine by command, or you can just download source code directly from [github page](https://github.com/RavenfieldCommunity/RavenM):
   
```bash
git clone https://github.com/RavenfieldCommunity/RavenM.git
git checkout master
```
    
Pay attention to the branch name, whether you want stable or beta branches!

Then build:
```bash
dotnet build RavenM
```

Dependencies should be restored when building. If not, run the following command:
```bash
dotnet restore
```

**If your network is too bad to connent to the [BepInEX NuGet service](https://nuget.bepinex.dev)**, please dwonload the packages at another device and set up the local NuGet source, like:

```bash
dotnet nuget add source "D:/LocalNugetPackages" -n LocalNugetPackages
```

Then put the packages to the folder you typed in the command(`D:/LocalNugetPackages`), build project as normal.

## Commit message

Commit message standard is not enforced but recommended.

Example formats e.g.： `feat&fix: new triggers, fixed bug when enter game`, `chore: updated deps`, `merge: EA32`

Available tags: `feat`, `fix`, `chore`, `merge`



