# RavenM

A Ravenfield multiplayer mod.

[![Discord](https://img.shields.io/discord/458403487982682113.svg?label=Discord&logo=Discord&colorB=7289da&style=for-the-badge)](https://discord.gg/63zE4gY)

## This mod is very **W.I.P.** There are a lot of bugs and opportunities to crash, so please report anything you find!

# Installing&details

More details, please go to https://ravenfieldcommunity.github.io/docs/en/Projects/ravenm.html

**Important Note:** RavenM does not support BepInEx version 6. Please ensure to install the latest version of BepInEx 5.x.x to complete the installation.

This mod depends on [BepInEx](https://github.com/BepInEx/BepInEx), a cross-platform Unity modding framework. 

First, install BepInEx into Ravenfield following the installation instructions [here](https://docs.bepinex.dev/articles/user_guide/installation/index.html). As per the instructions, make sure to run the game at least once with BepInEx installed before adding the mod to generate config files.

Next, Download RavenM [here](https://github.com/RavenfieldCommunity/RavenM/releases/latest) and unzip the file, place `RavenM.dll` into `Ravenfield/BepInEx/plugins/`. Optionally, you may also place `RavenM.pdb` to help us debug when there is.

Run the game and RavenM should now be installed.

# Building from source

Visual Studio 2019+ is recommended. 

.NET SDK 4.6 or newer is required.

Steps to build:

1. Clone the repository to your local machine
   
    ```bash
    git clone https://github.com/RavenfieldCommunity/RavenM.git
    git checkout master
    ```

2. Build project

    ```bash
    dotnet build RavenM
    ```

    Dependencies should be restored when building. If not, run the following command:

    ```bash
    dotnet restore
    ```
