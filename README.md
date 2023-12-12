# BUCK Data Management

_BUCK Data Management_ is BUCK's Unity package for saving and loading data asynchronously. It includes a `DataManager` class that provides an API for saving and loading data and an `ISaveable` interface that can be implemented on any class that needs to be saved and loaded.

## Requirements

This package works with Unity 2023.1 and above as it requires Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable.html) class for asynchronous saving and loading.

## Installation

### Install Unity Converters for Newtonsoft.Json (strongly recommended)

This package depends on Unity's Json.NET package for serializing data, which is already included as a package dependency and will be installed automatically. Out of the box, it supports saving basic C# types like ints, strings, and collections.

However, Unity types like Vector3 don't serialize to JSON very nicely because all of the properties are included, some of which are recursive. It is strongly recommended that you install the [Unity Converters for Newtonsoft.Json](https://github.com/applejag/Newtonsoft.Json-for-Unity.Converters) package. Once you've done this, Json.NET should be able to convert Unity's built-in types.

### Install Package

1. Copy the git URL of this repository: `https://github.com/buck-co/unity-pkg-data-management.git`
2. In Unity, open the Package Manager from the menu by going to `Window > Package Manager`
3. Click the plus icon in the upper left and choose `Add package from git URL...`
4. Paste the git URL into the text field and click the `Add` button.

### Setup your Project

If you want to see an example of how the Data Management package can be used, install the Samples from the package manager. This will add a "Samples" folder to your project where you can benchmark the Data Manager's methods.

If you'd rather get started right away without looking at the sample, follow these steps:
1. Add the Data Manager component to a GameObject in your scene. You can name the GameObject something like "Data Manager"
2. Implement the ISaveable interface on at least once class. (Again, check out the samples to see example implementations!)
3. Call DataManager API methods like `DataManager.Save()` from elsewhere in your project, such as from a Game Manager class.

## What's Included

### DataManager API

Still WIP! More documentation will go here once the dust settles, but JSON reading and writing works. Classes that implement the ISaveable interface need to register themselves with the DataManager currently.

### Asynchronous Saving and Loading

Async methods are working, although reads and writes have room for performance improvement. In progress!

### Encryption

Basic XOR encryption is implemented. AES encryption is planned.

### Data Migrations

Not yet started, but planned! This will allow individual objects to include a version number and upgrade themselves with game patches.

### Data Adaptors

Not yet started, but planned! This will allow consumers of the DataManager to use platforms not supported by Unity out of the box.
  
