# BUCK Data Management

## About
_BUCK Data Management_ is BUCK's Unity package designed for asynchronous saving and loading of game data. Utilizing Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable) class, it allows for smooth non-blocking file operations in Unity games.

### Features
- :watch: **Asynchronous**: Leverages Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable) class for non-blocking file I/O
- :zap: **DataManager API**: A suite of static methods for saving and loading data easily
- :floppy_disk: **ISaveable Interface**: A simple interface that can make any object in your game "saveable" (and "loadable") by capturing and restoring state
- :ledger: **JSON Formatting**: Automatic serialization to and from JSON using the [Newtonsoft Json Unity Package](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)



## Requirements

This package works with Unity 2023.1 and above as it requires Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable.html) class for asynchronous saving and loading.

## Installation

### Install the Package

1. Copy the git URL of this repository: `https://github.com/buck-co/unity-pkg-data-management.git`
2. In Unity, open the Package Manager from the menu by going to `Window > Package Manager`
3. Click the plus icon in the upper left and choose `Add package from git URL...`
4. Paste the git URL into the text field and click the `Add` button.

### Install Unity Converters for Newtonsoft.Json (strongly recommended)

This package depends on Unity's Json.NET package for serializing data, which is already included as a package dependency and will be installed automatically. Out of the box, it supports saving basic C# types like ints, strings, and collections.

However, Unity types like Vector3 don't serialize to JSON very nicely because all of the properties are included, some of which are recursive. It is strongly recommended that you install the [Unity Converters for Newtonsoft.Json](https://github.com/applejag/Newtonsoft.Json-for-Unity.Converters) package. Once you've done this, Json.NET should be able to convert Unity's built-in types.

## Getting Started

### Included Samples

This package includes a sample project which you can install from the Unity Package Manager by selecting the package from the list and then selecting the `Samples` tab on the right. Then click `Import`. Examining the sample can help you understand how to use the DataManagement package in your own project.

### Quick Setup

Here's how the package can be used.
1. Add the Data Manager component to a GameObject in your scene.
2. Implement the `ISaveable` interface on at least one class. (more detail on how to do this is available below).
3. Call DataManager API methods like `DataManager.Save()` from elsewhere in your project, such as from a Game Manager class. Do this _after_ all your ISaveable implementations are registered with the DataManager.


## Saving and Loading Classes using the `ISaveable` Interface

Any class that should save or load data needs to implement the [`ISaveable`](Runtime/ISaveable.cs) interface which contains key elements that enable an object to be saved and loaded.

- **Guid Property**: Each `ISaveable` must have a globally unique identifier (Guid) for distinguishing it when saving and loading data.
- **Filename Property**: Each `ISaveable` must have a filename string that identifies which file it should be saved in.
- **CaptureState Method**: This method captures and returns the current state of the object in a serializable format.
- **RestoreState Method**: This method restores the object's state from the provided data.

### Example Implementation: `GameDataExample`

1. **Implement `ISaveable` in Your Class**
    <br>Your class should inherit from `ISaveable` from the `Buck.DataManagement` namespace.
    ```csharp
    using Buck.DataManagement

    public class YourClass : MonoBehaviour, ISaveable
    {
        // Your code here...
    }
    ```

2. **Choose a Filename**
    <br>This is the file name where this object's data will be saved.
    ```csharp
    public string FileName => Files.GameData;
    ```

    It is recommended to use a static class to store file paths as strings to avoid typos.

    ```csharp
    public static class Files
    {
        public const string GameData = "GameData.dat";
        public const string SomeFile = "SomeFile.dat";
    }
    ```

3. **Generate and Store a Unique Serializable Guid**
    <br>Ensure that your class has a globally unique identifier (a GUID for short). Use the GetSerializableGuid class in the data manager to make sure that your MonoBehaviours and other classes can be identified when being saved and loaded.
    ```csharp
    [SerializeField, HideInInspector] byte[] m_guidBytes;
    public Guid Guid => new(m_guidBytes);
    void OnValidate() => DataManager.GetSerializableGuid(ref m_guidBytes);
    ```

4. **Register Your Object with `DataManager`**
    <br>Register the object with `DataManager`. Generally it's best to do this in your `Awake` method or during initialization. Make sure you do this before calling any save or load methods in the DataManager or your saveables won't be picked up!
    ```csharp
    void Awake()
    {
        DataManager.RegisterSaveable(this);
    }
    ```

5. **Define Your Data Structure**
    <br>Create a struct or class that represents the data you want to save. This structure needs to be serializable.
    ```csharp
    [Serializable]
    public struct MyCustomData
    {
        // Custom data fields
        public string playerName;
        public int playerHealth;
        public Vector3 position;
        public List<Enemy> enemies;
        public Dictionary<int, Item> inventory;
    }
    ```

6. **Implement `CaptureState` and `RestoreState` Methods**
    <br>Implement the `CaptureState` method to capture and return the current state of your object. Then implement the `RestoreState` method to restore your object's state from the saved data. both of these methods will be called by the `DataManager` when you call its save and load methods.
    ```csharp
    public object CaptureState()
    {
        return new MyCustomData
        {
            playerName = m_playerName,
            playerHealth = m_playerHealth,
            position = m_position,
            enemies = m_enemies,
            inventory = m_inventory
        };
    }

    public void RestoreState(object state)
    {
        var s = (MyCustomData)state;

        m_playerName = s.playerName;
        m_playerHealth = s.playerHealth;
        m_position = s.position;
        m_enemies = s.enemies;
        m_inventory = s.inventory;
    }
    ```

For a complete example, check out [this ISaveable implementation](Samples~/GameDataExample.cs) in the sample project.

## DataManager API

The [`DataManager`](Runtime/DataManager.cs) is intended to be called from another class in your project, such as a Game Manager, or anywhere that you would like to do saving and loading operations. You should add the DataManager as a component to a GameObject in your scene (you can name the GameObject something like `Data Manager`). Below you'll find the public interface for interactnig with the DataManager class, along with short code examples.

**Note:** The `DataManager` is in the `Buck.DataManagement` namespace. Be sure to include this line at the top of any files that make calls to the DataManager.

```csharp
using Buck.DataManagement
```
<br>


### Properties

#### `bool IsBusy`
Indicates whether or not the DataManager is currently busy with a file operation. This can be useful if you want to wait for one operation to finish before doing another, although because file operations are queued, this generally is only necessary for benchmarking and testing purposes.
<br>

**Usage Example**:
  ```csharp
  while (DataManager.IsBusy)
  await Awaitable.NextFrameAsync();
  ```
<br>

### Methods

#### `void RegisterSaveable(ISaveable saveable)`
Registers an ISaveable with the DataManager for saving and loading.

**Usage Example**:
  ```csharp
  DataManager.RegisterSaveable(mySaveableObject);
  ```
<br>

#### `Awaitable SaveAsync(string[] filenames)`
Asynchronously saves the files at the specified array of paths or filenames.

**Usage Example**:
  ```csharp
  await DataManager.SaveAsync(new string[] {"MyFile.dat"});
  ```
<br>

#### `Awaitable LoadAsync(string[] filenames)`
Asynchronously loads the files at the specified array of paths or filenames.

**Usage Example**:
  ```csharp
  await DataManager.LoadAsync(new string[] {"MyFile.dat"});
  ```
<br>

#### `Awaitable DeleteAsync(string[] filenames)`
Asynchronously deletes the files at the specified array of paths or filenames.

**Usage Example**:
  ```csharp
  await DataManager.DeleteAsync(new string[] {"MyFile.dat"});
  ```
<br>

#### `Awaitable EraseAsync(string[] filenames)`
Asynchronously erases the files at the specified paths or filenames, leaving them empty but still on disk.

**Usage Example**:
  ```csharp
  await DataManager.EraseAsync(new string[] {"MyFile.dat"});
  ```
<br>

#### `byte[] GetSerializableGuid(ref byte[] guidBytes)`
Sets the given Guid byte array to a new Guid byte array if it is null, empty, or an empty Guid. The `guidBytes` parameter is a byte array (passed by reference) that you would like to fill with a serializable guid.

**Usage Example**:
  ```csharp
  [SerializeField, HideInInspector] byte[] m_guidBytes;
  public Guid Guid => new(m_guidBytes);
  void OnValidate() => DataManager.GetSerializableGuid(ref m_guidBytes);
  ```
<br>

## Best Practices

- **Consistent State Management**: Ensure that your `CaptureState` and `RestoreState` methods consistently manage the object's state. If you update your data structure, be sure to update these methods as well.
- **Efficient Data Structures**: Generally speaking it's best to avoid deep serialization structures (such as a list of arrays of lists of dictionaries of structs, etc.). It's also a good idea to use simple data types like ints, strings, and structs rather than storing Unity types like GameObjects or ScriptableObjects.
- **Guid Management**: Manage Guids carefully to ensure uniqueness and avoid conflicts. The example given above should make this fairly straightforward.

## Encryption

If you want to prevent mischeivious gamers from tampering with your save files, you can encrypt them using XOR encryption. To turn it on, use the encryption dropdown menu on the DataManager component in your scene and create a password. XOR is very basic and can be hacked using brute force methods, but it is very fast. AES encryption is planned!

## Additional Project Information

### Why did we build this?
Figuring out how to save and load your game data can be tricky, but what's even more challenging is deciding _when_ to save your game data. Not only is there the issue of data serialization and file I/O, but in addition, save and load operations often end up happening synchronously on Unity's main thread which will cause framerate dips. That's because Unity's renderer is also on the main thread! Furthermore, while most desktops have fast SSDs, sometimes file I/O can take longer than the time it takes to render a frame, especially if you're running your game on a gaming console or a computer with an HDD.

We hit these pain points on our game _[Let's! Revolution!](https://store.steampowered.com/app/2111090/Lets_Revolution/)_ and we wanted to come up with a better approach. By combining [`async`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/async) and [`await`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/await) with Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable.html) class (available in Unity 2023.1 and up), it is now possible to do file operations both asynchronously _and_ on background threads. That means you can save and load data in the background while your game continues to render frames seamlessly. Nice! However, there's still a good bit to learn about how multithreading works in the context of Unity and how to combine that with a JSON serializer and other features like encryption. The _BUCK Data Management_ package aims to take care of these complications and make asynchronous saving and loading data in Unity a breeze!

## Feedback and Contributing

If you have any trouble using the package, feel free to [open an issue](https://github.com/buck-co/unity-pkg-data-management/issues). And if you're interested in contributing, [create a pull request](https://github.com/buck-co/unity-pkg-data-management/pulls) and we'll take a look!

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [tags on this repository](https://github.com/buck-co/unity-pkg-data-management/tags). 

## Authors

* **Nick Pettit** - [nickpettit](https://github.com/nickpettit)

See also the list of [contributors](https://github.com/buck-co/unity-pkg-data-management/contributors) who participated in this project.

## Acknowledgments

* Thanks to [Tarodev for this tutorial](https://www.youtube.com/watch?v=X9Dtb_4os1o) on using async and await in Unity using the Awaitable class. It gave me the idea for creating an async save system.
* Thanks to [Dapper Dino for this tutorial](https://www.youtube.com/watch?v=f5GvfZfy3yk) which demonstrated how a form of the inversion of control design pattern could be used to make saving and loading easier.
* Thanks to [Bronson Zgeb at Unity for this Unity talk](https://www.youtube.com/watch?v=uD7y4T4PVk0) which shows many of the pieces necessary for building a save system in Unity.


## License

MIT License - Copyright (c) 2023 BUCK Design LLC [buck-co](https://github.com/buck-co)