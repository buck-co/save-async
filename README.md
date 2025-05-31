![opt 03](https://github.com/buck-co/save-async/assets/41124/f1a6ebbc-41aa-49e0-9ae9-5daf4e5bb228)

# Save Async
_Save Async_ is [BUCK](https://buck.co)'s Unity package for asynchronously saving and loading data in the background using Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable) class. It includes a simple API that makes it easy to capture and restore state without interrupting Unity's main render thread. That means smoother framerates and fewer load screens!

### Features
- :watch: **Asynchronous**: All methods are asynchronously "awaitable" meaning the game can continue running while it waits for a response from the storage device, even if that storage device is slow (like HDDs, external storage, and some consoles)
- :thread: **Background Threading**: All file I/O occurs on background threads which helps avoid dips in framerate
- :zap: **SaveManager API**: Simple API that can be called from anywhere with methods like `SaveManager.Save()`
- :floppy_disk: **ISaveable Interface**: Implement this interface on any class to give it the ability to be saved and loaded
- :ledger: **JSON Serialization**: Data is saved to JSON automatically using Unity's own [Newtonsoft Json Unity Package](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)
- :game_die: **Battle-Tested**: Used in production on shipped games including [_The Electric State: Kid Cosmo_](https://apps.apple.com/us/app/the-electric-state-kid-cosmo/id6475495298)

# Getting Started
> [!NOTE]
> This package works with **Unity 2023.1 and above** as it requires Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable.html) class which is not available in earlier versions.>

📺 [Watch the Save Async Tutorial video](https://vimeo.com/buck/save-async) or follow the instructions below.

### Install the _Save Async_ Package

1. Copy the git URL of this repository: `https://github.com/buck-co/save-async.git`
2. In Unity, open the Package Manager from the menu by going to `Window > Package Manager`
3. Click the plus icon in the upper left and choose `Add package from git URL...`
4. Paste the git URL into the text field and click the `Add` button.

### Install the _Unity Converters for Newtonsoft.Json_ Package (strongly recommended)

1. Copy this git URL: `https://github.com/applejag/Newtonsoft.Json-for-Unity.Converters.git#upm`
2. In Unity, open the Package Manager from the menu by going to `Window > Package Manager`
3. Click the plus icon in the upper left and choose `Add package from git URL...`
4. Paste the git URL into the text field and click the `Add` button.

Save Async depends on Unity's Json.NET package for serializing data, which is already included as a package dependency and will be installed automatically. However, Unity types like Vector3 don't serialize to JSON very nicely, and can include ugly recursive property loops, like this:

```json
{
  "x": 0,
  "y": 1,
  "z": 0,
  "normalized": {
    "x": 0,
    "y": 1,
    "z": 0,
    "normalized": {
      "x": 0,
      "y": 1,
      "z": 0,
      "normalized": {
        "x": 0,
        "y": 1,
        "z": 0,
        "normalized": {
          ...
        }
      }
    }
  }
}
```

_Yikes!_ Installing the [Unity Converters for Newtonsoft.Json](https://github.com/applejag/Newtonsoft.Json-for-Unity.Converters) package takes care of these issues, as well as many more.

Once you've done this, Json.NET should be able to convert Unity's built-in types. In the future, we'll try to include this as a package dependency, but currently the Unity Package Manager only allows packages to have dependencies that come from the official Unity registry.

### Basic Workflow
After installing the package...

1. Add the `SaveManager` component to a GameObject in your scene.
2. Implement the `ISaveable` interface on at least one class (more detail on how to do this is available below).
3. Register the ISaveable by calling `SaveManager.RegisterSaveable(mySaveableObject);` This is usually done in `MonoBehaviour.Awake()`
4. Call SaveManager API methods like `SaveManager.Save()` from elsewhere in your project, such as from a Game Manager class. Do this _after_ all your ISaveable implementations are registered.

### Included Samples

This package includes a sample project which you can install from the Unity Package Manager by selecting the package from the list and then selecting the `Samples` tab on the right. Then click `Import`. Examining the sample can help you understand how to use the package in your own project.


# Implementing ISaveable and using the SaveManager API

Any class that should save or load data needs to implement the [`ISaveable`](Runtime/ISaveable.cs) interface.

- **Key Property**: Each `ISaveable` must have a globally unique string for distinguishing it when saving and loading data.
- **Filename Property**: Each `ISaveable` must have a filename string that identifies which file it should be saved in.
- **CaptureState Method**: This method captures and returns the current state of the object in a serializable format.
- **RestoreState Method**: This method restores the object's state from the provided data.

## Example Implementation: `GameDataExample`

1. **Implement `ISaveable` in Your Class**
    <br>Your class should inherit from `ISaveable` from the `Buck.SaveAsync` namespace.
    ```csharp
    using Buck.SaveAsync

    public class YourClass : MonoBehaviour, ISaveable
    {
        // Your code here...
    }
    ```

2. **Choose a Filename**
    <br>This is the file name where this object's data will be saved.
    ```csharp
    public string Filename => Files.GameData;
    ```

    It is recommended to use a static class to store file paths as strings to avoid typos.

    ```csharp
    public static class Files
    {
        public const string GameData = "GameData.dat";
        public const string SomeFile = "SomeFile.dat";
    }
    ```

3. **Generate and Store a Unique Serializable Key**
    <br>Ensure that your class has a globally unique string key, such as "GameDataExample".
    ```csharp
    public string Key => "GameDataExample";
    ```
    Optionally, you can generate and use a serializable Guid to uniquely identify your objects. Use `SaveManager.GetSerializableGuid()` in MonoBehaviour.OnValidate() to get the Guid and then store it as a serialized byte array (since the System.Guid type itself cannot be serialized).
    ```csharp
        [SerializeField, HideInInspector] byte[] m_guidBytes;
        public string Key => new Guid(m_guidBytes).ToString();
        void OnValidate() => SaveManager.GetSerializableGuid(ref m_guidBytes);
    ```

4. **Register Your Object with `SaveManager`**
    <br>Register the object with `SaveManager`. Generally it's best to do this in your `Awake` method or during initialization. Make sure you do this before calling any save or load methods in the SaveManager class or your saveables won't be picked up!
    ```csharp
    void Awake()
    {
        SaveManager.RegisterSaveable(this);
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
        public Dictionary<int, Item> inventory;
    }
    ```

6. **Implement `CaptureState` and `RestoreState` Methods**
    <br>Implement the `CaptureState` method to capture and return the current state of your object. Then implement the `RestoreState` method to restore your object's state from the saved data. Both of these methods will be called by the `SaveManager` class when you call its save and load methods.
    ```csharp
    public object CaptureState()
    {
        return new MyCustomData
        {
            playerName = m_playerName,
            playerHealth = m_playerHealth,
            position = m_position,
            inventory = m_inventory
        };
    }

    public void RestoreState(object state)
    {
        var s = (MyCustomData)state;

        m_playerName = s.playerName;
        m_playerHealth = s.playerHealth;
        m_position = s.position;
        m_inventory = s.inventory;
    }
    ```

For a complete example, check out [this ISaveable implementation](Samples~/SaveAsyncExample/GameDataExample.cs) in the sample project.

## SaveManager API

[`SaveManager`](Runtime/SaveManager.cs) methods can be called anywhere in your game's logic that you want to save or load, such as in a Game Manager class or a main menu screen. You should add the SaveManager component to a GameObject in your scene. Below you'll find the public interface for interacting with the SaveManager class, along with short code examples.

> [!NOTE]
> The `SaveManager` class is in the `Buck.SaveAsync` namespace. Be sure to include this line at the top of any files that make calls to SaveManager methods or implement the ISaveable interface.
```csharp
using Buck.SaveAsync
```
<br>


### Properties

#### `bool IsBusy`
Indicates whether or not the SaveManager class is currently busy with a file operation. This can be useful if you want to wait for one operation to finish before doing another, although because file operations are queued, this generally is only necessary for benchmarking and testing purposes.
<br>

**Usage Example**:
  ```csharp
  while (SaveManager.IsBusy)
  await Awaitable.NextFrameAsync();
  ```
<br>

### Methods

#### `void RegisterSaveable(ISaveable saveable)`
Registers an ISaveable with the SaveManager class for saving and loading.

**Usage Example**:
  ```csharp
  SaveManager.RegisterSaveable(mySaveableObject);
  ```
<br>

#### `Awaitable Save(string[] filenames)`
Asynchronously saves the files at the specified array of paths or filenames.

**Usage Example**:
  ```csharp
  await SaveManager.Save(new string[] {"MyFile.dat"});
  ```
<br>

#### `Awaitable Load(string[] filenames)`
Asynchronously loads the files at the specified array of paths or filenames.

**Usage Example**:
  ```csharp
  await SaveManager.Load(new string[] {"MyFile.dat"});
  ```
<br>

#### `Awaitable Delete(string[] filenames)`
Asynchronously deletes the files at the specified array of paths or filenames.

**Usage Example**:
  ```csharp
  await SaveManager.Delete(new string[] {"MyFile.dat"});
  ```
<br>

#### `Awaitable Erase(string[] filenames)`
Asynchronously erases the files at the specified paths or filenames, leaving them empty but still on disk.

**Usage Example**:
  ```csharp
  await SaveManager.Erase(new string[] {"MyFile.dat"});
  ```
<br>

#### `byte[] GetSerializableGuid(ref byte[] guidBytes)`
Sets the given Guid byte array to a new Guid byte array if it is null, empty, or an empty Guid. The `guidBytes` parameter is a byte array (passed by reference) that you would like to fill with a serializable guid.

**Usage Example**:
  ```csharp
  [SerializeField, HideInInspector] byte[] m_guidBytes;
  public string Key => new Guid(m_guidBytes).ToString();
  void OnValidate() => SaveManager.GetSerializableGuid(ref m_guidBytes);
  ```
<br>

## Encryption

If you want to prevent mischievous gamers from tampering with your save files, you can encrypt them using XOR encryption. To turn it on, use the encryption dropdown menu on the SaveManager component in your scene and create a password. XOR is very basic and can be hacked using brute force methods, but it is very fast. AES encryption is planned!

# Additional Project Information

### Why did we build this?
Figuring out how to save and load your game data can be tricky, but what's even more challenging is deciding _when_ to save your game data. Not only is there the issue of data serialization and file I/O, but in addition, save and load operations often end up happening synchronously on Unity's main thread which will cause framerate dips. That's because Unity's renderer is also on the main thread! Furthermore, while most desktops have fast SSDs, sometimes file I/O can take longer than the time it takes to render a frame, especially if you're running your game on a gaming console or a computer with an HDD.

We hit these pain points on our game _[Let's! Revolution!](https://store.steampowered.com/app/2111090/Lets_Revolution/)_ and we wanted to come up with a better approach. By combining [`async`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/async) and [`await`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/await) with Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable.html) class (available in Unity 2023.1 and up), it is now possible to do file operations both asynchronously _and_ on background threads. That means you can save and load data in the background while your game continues to render frames seamlessly. Nice! However, there's still a good bit to learn about how multithreading works in the context of Unity and how to combine that with a JSON serializer and other features like encryption. The _Save Async_ package aims to take care of these complications and make asynchronous saving and loading data in Unity a breeze!

### Why not just use Coroutines?
While Coroutines have served us well for many years, the Task-based asynchronous pattern (TAP) enabled by async/await and Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable) class has many advantages. Coroutines can execute piece by piece over time, but they still process on Unity's single main thread. If a Coroutine attempts a long-running operation (like accessing a file) it can cause the whole application to freeze for several frames. For a good overview of the differences between async/await and Coroutines, check out this Unite talk [Best practices: Async vs. coroutines - Unite Copenhagen](https://www.youtube.com/watch?v=7eKi6NKri6I&t=548s).

# Contributing

Found a bug or have a feature request? We'd love to hear from you!

- [Open an issue](https://github.com/buck-co/save-async/issues) for problems or suggestions
- [Create a pull request](https://github.com/buck-co/save-async/pulls) if you'd like to contribute code
- Check our [contribution guidelines](CONTRIBUTING.md) before submitting

# Authors

* **Nick Pettit** - [nickpettit](https://github.com/nickpettit)

See the full list of [contributors](https://github.com/buck-co/save-async/contributors).

# Acknowledgments

* Thanks to [Tarodev for this tutorial](https://www.youtube.com/watch?v=X9Dtb_4os1o) on using async and await in Unity using the Awaitable class. It gave me the idea for creating an async save system.
* Thanks to [Dapper Dino for this tutorial](https://www.youtube.com/watch?v=f5GvfZfy3yk) which demonstrated how a form of the inversion of control design pattern could be used to make saving and loading easier.
* Thanks to [Bronson Zgeb at Unity for this Unite talk](https://www.youtube.com/watch?v=uD7y4T4PVk0) which shows many of the pieces necessary for building a save system in Unity.

# License

MIT License - Copyright (c) 2025 BUCK Design LLC [buck-co](https://github.com/buck-co)

---

_[BUCK](https://buck.co) is a global creative company that brings brands, stories, and experiences to life through art, design, and technology. If you're a Game Developer or Creative Technologist or want to get involved with our work, reach out and say hi via [Github](https://github.com/buck-co), [Instagram](https://www.instagram.com/buck_design/?hl=en) or our [Careers page](https://buck.co/careers). 👋_
