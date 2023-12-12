# BUCK Data Management

## About
_BUCK Data Management_ is BUCK's Unity package designed for efficient and asynchronous saving and loading of game data. Utilizing Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable) class, it allows for smooth and non-blocking file operations in Unity games.

### Features
- **Asynchronous**: Leverages Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable) class for non-blocking file I/O
- **DataManager API**: A suite of static methods for saving and loading data easily
- **ISaveable Interface**: A standardized interface for objects that can be saved and loaded
- **JSON Formatting**: Files are serialized to JSON using the [Newtonsoft Json Unity Package](https://docs.unity3d.com/Packages/com.unity.nuget.newtonsoft-json@3.2/manual/index.html)



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

### Included Examples

This package includes a sample project which you can install from the Unity Package Manager by selecting the package from the list and then selecting the `Samples` tab on the right. Then click `Import`. This will add a "Samples" folder to your project where you can benchmark the Data Manager's methods. In the samples, you'll find two examples of how you can implement the ISaveable class which can help you get an idea of how you might implement ISaveable on your own classes.

### Quick Project Setup

Here's how the package can be used.
1. Add the Data Manager component to a GameObject in your scene. You can name the GameObject something like `Data Manager`.
2. Implement the ISaveable interface on at least one class. Most importantly, make sure that class's ISaveable.Guid property is backed by a persistent serializable Guid. (more detail on this is available below).
3. Call `DataManager.RegisterSaveable` on your object (ideally this is done in Awake).
4. Call DataManager API methods like `DataManager.Save()` from elsewhere in your project, such as from a Game Manager class. Do this _after_ all your ISaveable implementations are registered with the DataManager.


## Saving and Loading Classes using the `ISaveable` Interface

Any class that should save or load data needs to implement the `ISaveable` interface which contains key elements that enable an object to be saved and loaded:

1. **Guid Property**: Each `ISaveable` must have a unique identifier (Guid) for distinguishing it when saving and loading data.
2. **CaptureState Method**: This method captures and returns the current state of the object in a serializable format.
3. **RestoreState Method**: This method restores the object's state from the provided data.

### Example Implementation: `GameDataExample`

In the `GameDataExample` class, `ISaveable` is implemented as follows:

1. **Guid Property**: A byte array `m_guidBytes` is serialized and used to generate the Guid.
    ```csharp
    [SerializeField, HideInInspector] byte[] m_guidBytes;
    public Guid Guid => new Guid(m_guidBytes);
    ```

2. **CaptureState Method**: It captures the current state of the object and returns it.
    ```csharp
    public object CaptureState() {
        return new MyCustomData {
            playerName = m_playerName,
            playerHealth = m_playerHealth,
            // Additional data fields...
        };
    }
    ```

3. **RestoreState Method**: It restores the object's state from the provided data.
    ```csharp
    public void RestoreState(object state) {
        var s = (MyCustomData)state;
        m_playerName = s.playerName;
        m_playerHealth = s.playerHealth;
        // Additional data fields...
    }
    ```

## Steps to Implement `ISaveable`

1. **Implement `ISaveable` in Your Class**: Your class should inherit from `ISaveable`.
    ```csharp
    public class YourClass : MonoBehaviour, ISaveable
    {
        // Implementation details...
    }
    ```

2. **Define Your Data Structure**: Create a struct or class that represents the data you want to save. This structure needs to be serializable.
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

3. **Generate and Store a Unique Serializable Guid**: Ensure that your class has a globally unique identifier (a GUID for short). Use the GetSerializableGuid class in the data manager to make sure that your MonoBehaviours and other classes can be identified when being saved and loaded.
    ```csharp
    [SerializeField, HideInInspector] byte[] m_guidBytes;
    public Guid Guid => new(m_guidBytes);
    void OnValidate() => DataManager.GetSerializableGuid(ref m_guidBytes);
    ```

4. **Implement `CaptureState` Method**: Implement this method to capture and return the current state of your object.
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
    ```

5. **Implement `RestoreState` Method**: Implement this method to restore your object's state from the saved data.
    ```csharp
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

6. **Register Your Object with `DataManager`**: Register the object with `DataManager`. Generally it's best to do this in your `Awake` method or during initialization. Make sure you do this before calling any save or load methods in the DataManager or your saveables won't be picked up!
    ```csharp
    void Awake()
    {
        DataManager.RegisterSaveable(this);
    }
    ```

For a complete example, check out [this ISaveable implementation](https://github.com/buck-co/unity-pkg-data-management/blob/main/Samples~/GameDataExample.cs) in the sample project.


## DataManager API

The DataManager is intended to be called from another class in your project, such as a Game Manager, or anywhere that you would like to do saving and loading operations. You should add the DataManager as a component to a GameObject in your scene (you can name the GameObject something like `Data Manager`). Below you'll find the public interface for interactnig with the DataManager class, along with short code examples.

### Properties
- **IsBusy**
  - Type: `bool`
  - Description: Indicates whether the DataManager is currently busy with a file operation.

### Methods

#### RegisterSaveable
```csharp
public static void RegisterSaveable(ISaveable saveable)
```
- **Description**: Registers an ISaveable with the DataManager for saving and loading.
- **Parameters**:
  - `saveable`: The ISaveable object to register.
- **Usage Example**:
  ```csharp
  DataManager.RegisterSaveable(mySaveableObject);
  ```

#### SaveAsync
```csharp
public static async Awaitable SaveAsync(string[] filenames)
```
- **Description**: Asynchronously saves the files at the specified paths or filenames.
- **Parameters**:
  - `filenames`: Array of paths or filenames to save.
- **Usage Example**:
  ```csharp
  await DataManager.SaveAsync(new string[] {"MyFile.dat"});
  ```

#### LoadAsync
```csharp
public static async Awaitable LoadAsync(string[] filenames)
```
- **Description**: Asynchronously loads the files at the specified paths or filenames.
- **Parameters**:
  - `filenames`: Array of paths or filenames to load.
- **Usage Example**:
  ```csharp
  await DataManager.LoadAsync(new string[] {"MyFile.dat"});
  ```

#### DeleteAsync
```csharp
public static async Awaitable DeleteAsync(string[] filenames)
```
- **Description**: Asynchronously deletes the files at the specified paths or filenames.
- **Parameters**:
  - `filenames`: Array of paths or filenames to delete.
- **Usage Example**:
  ```csharp
  await DataManager.DeleteAsync(new string[] {"MyFile.dat"});
  ```

#### EraseAsync
```csharp
public static async Awaitable EraseAsync(string[] filenames)
```
- **Description**: Asynchronously erases the files at the specified paths or filenames, leaving them empty but still on disk.
- **Parameters**:
  - `filenames`: Array of paths or filenames to erase.
- **Usage Example**:
  ```csharp
  await DataManager.EraseAsync(new string[] {"MyFile.dat"});
  ```

#### GetSerializableGuid
```csharp
public static byte[] GetSerializableGuid(ref byte[] guidBytes)
```
- **Description**: Sets the given Guid byte array to a new Guid byte array if it is null, empty, or an empty Guid.
- **Parameters**:
  - `guidBytes`: The byte array (passed by reference) that you would like to fill with a serializable guid.
- **Usage Example**:
  ```csharp
  [SerializeField, HideInInspector] byte[] m_guidBytes;
  public Guid Guid => new(m_guidBytes);
  void OnValidate() => DataManager.GetSerializableGuid(ref m_guidBytes);
  ```

## Best Practices

- **Consistent State Management**: Ensure that your `CaptureState` and `RestoreState` methods consistently manage the object's state. If you update your data structure, be sure to update these methods as well.
- **Efficient Data Structures**: Generally speaking it's best to avoid deep serialization structures (such as a list of arrays of lists of dictionaries of structs, etc.). It's also a good idea to use simple data types like ints, strings, and structs rather than storing Unity types like GameObjects or ScriptableObjects.
- **Guid Management**: Manage Guids carefully to ensure uniqueness and avoid conflicts. The example given above should make this fairly straightforward.

## Encryption

Basic XOR encryption is implemented. AES encryption is planned.

## Additional Project Information

### Why did you build this?
Figuring out how to save and load your game data can be tricky, but what's even more challenging is deciding _when_ to save your game data. Not only is there the issue of data serialization and file I/O, but in addition, save and load operations often end up happening synchronously on Unity's main thread which will cause framerate dips. That's because Unity's renderer is also on the main thread! Furthermore, while most desktops have fast SSDs, sometimes file I/O can take longer than the time it takes to render a frame, especially if you're running your game on a gaming console or a computer with an HDD.

We hit these pain points on our game _[Let's! Revolution!](https://store.steampowered.com/app/2111090/Lets_Revolution/)_ and we wanted to come up with a better approach. By combining [`async`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/keywords/async) and [`await`](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/operators/await) with Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable.html) class (available in Unity 2023.1 and up), it is now possible to do file operations both asynchronously _and_ on background threads. That means you can save and load data in the background while your game continues to render frames seamlessly. Nice! However, there's still a good bit to learn about how multithreading works in the context of Unity and how to combine that with a JSON serializer and other features like encryption. The _BUCK Data Management_ package aims to take care of these complications and make asynchronous saving and loading data in Unity a breeze!

## Contributing

Please read [CONTRIBUTING.md](https://gist.github.com/PurpleBooth/b24679402957c63ec426) for details on our code of conduct, and the process for submitting pull requests to us.

## Versioning

We use [SemVer](http://semver.org/) for versioning. For the versions available, see the [tags on this repository](https://github.com/buck-co/unity-pkg-data-management/tags). 

## Authors

* **Nick Pettit** - *Initial work* - [nickpettit](https://github.com/nickpettit)

See also the list of [contributors](https://github.com/buck-co/unity-pkg-data-management/contributors) who participated in this project.

## Acknowledgments

* Thanks to [Tarodev for this tutorial](https://www.youtube.com/watch?v=X9Dtb_4os1o) on using async and await in Unity using the Awaitable class.

## License

MIT

## Roadmap

### Data Migrations

Not yet started, but planned! This will allow individual objects to include a version number and upgrade themselves with game patches, which makes it easier to change an object's data structure and still use a user's saved data on a game that has already shipped.

### Data Adaptors

Not yet started, but planned! This will allow consumers of the DataManager to use platforms not supported by Unity out of the box. By nature this might be tricky to provide an example for, since this is primarily intended for closed platforms (like consoles), so the example might have to be partially hypothetical.

## Send Feedback!

In the future we would love to support things like backups, cross-platform compatibility and cloud saves, save slots, loading bars, and more. If you have ideas, feel free to submit an issue to this repo. We would love to hear what you like and what you don't like about this package so that it can improve.