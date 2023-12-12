# BUCK Data Management

_BUCK Data Management_ is BUCK's Unity package for saving and loading JSON data asynchronously. It includes a `DataManager` class that provides an API for saving and loading data and an `ISaveable` interface that can be implemented on any class that needs to be saved and loaded.

Figuring out how to save game data can be a pain. Not only is there the issue of data serialization and file I/O, but worse, often times save and load operations end up happening synchronously on Unity's main thread, which will cause framerate dips. Furthermore, while most desktops have SSDs, sometimes file I/O can take longer than the time it takes to render a frame, especially if you're running your game on a gaming console or a computer with an HDD.

We hit these pain points on our game _[Let's! Revolution!](https://store.steampowered.com/app/2111090/Lets_Revolution/)_ and we wanted to come up with a better approach. By combining C#.NET support of async and await with Unity's new [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable.html) class, it is now possible to do file I/O both asynchronously _and_ on background threads. That means you can save and load data, in the background, while your game continues to render frames seamlessly. Nice! However, there's still a good bit to learn about how multithreading works in the context of Unity and how to combine that with a JSON serializer and other features like encryption. The _BUCK Data Management_ package aims to make asynchronous saving and loading data in Unity a breeze!

## Requirements

This package works with Unity 2023.1 and above as it requires Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable.html) class for asynchronous saving and loading.

## Installation


### Install Package

1. Copy the git URL of this repository: `https://github.com/buck-co/unity-pkg-data-management.git`
2. In Unity, open the Package Manager from the menu by going to `Window > Package Manager`
3. Click the plus icon in the upper left and choose `Add package from git URL...`
4. Paste the git URL into the text field and click the `Add` button.

### Install Unity Converters for Newtonsoft.Json (strongly recommended)

This package depends on Unity's Json.NET package for serializing data, which is already included as a package dependency and will be installed automatically. Out of the box, it supports saving basic C# types like ints, strings, and collections.

However, Unity types like Vector3 don't serialize to JSON very nicely because all of the properties are included, some of which are recursive. It is strongly recommended that you install the [Unity Converters for Newtonsoft.Json](https://github.com/applejag/Newtonsoft.Json-for-Unity.Converters) package. Once you've done this, Json.NET should be able to convert Unity's built-in types.

### Sample Project and Setup

This package includes a sample project which you can install from the Unity Package Manager by selecting the package from the list and then selecting the `Samples` tab on the right. Then click `Import`. This will add a "Samples" folder to your project where you can benchmark the Data Manager's methods. In the samples, you'll find two examples of how you can implement the ISaveable class which can help you get an idea of how you might implement ISaveable on your own classes.

Here's how the package can be used.
1. Add the Data Manager component to a GameObject in your scene. You can name the GameObject something like `Data Manager`.
2. Implement the ISaveable interface on at least one class. Most importantly, make sure that class's ISaveable.Guid property is backed by a serializable Guid. (Again, check out the samples to see example implementations!)
3. Call `DataManager.RegisterSaveable` on your object (ideally this is done in Awake).
4. Call DataManager API methods like `DataManager.Save()` from elsewhere in your project, such as from a Game Manager class. Do this _after_ all your ISaveable implementations are registered with the DataManager.

## Implementing ISaveable

Code example will go here!

## DataManager API

Still WIP! More documentation will go here once the dust settles, but JSON reading and writing works. Classes that implement the ISaveable interface need to register themselves with the DataManager currently.

### Asynchronous Saving and Loading

Async methods are working, although reads and writes have room for performance improvement. In progress!

### Encryption

Basic XOR encryption is implemented. AES encryption is planned.

## Roadmap

### Data Migrations

Not yet started, but planned! This will allow individual objects to include a version number and upgrade themselves with game patches, which makes it easier to change an object's data structure and still use a user's saved data on a game that has already shipped.

### Data Adaptors

Not yet started, but planned! This will allow consumers of the DataManager to use platforms not supported by Unity out of the box. By nature this might be tricky to provide an example for, since this is primarily intended for closed platforms (like consoles), so the example might have to be partially hypothetical.

## Send Feedback!

In the future we would love to support things like backups, cross-platform compatibility and cloud saves, save slots, loading bars, and more. If you have ideas, feel free to submit an issue to this repo. We would love to hear what you like and what you don't like about this package so that it can improve.