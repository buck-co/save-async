# BUCK Data Management

_BUCK Data Management_ is BUCK's Unity package for saving and loading data. It includes a `DataManager` class that provides an API for saving and loading data and an `ISaveable` interface that can be implemented on any class that needs to be saved and loaded.

## Requirements

This package works with Unity 2023.2 and above as it requires Unity's [`Awaitable`](https://docs.unity3d.com/2023.2/Documentation/ScriptReference/Awaitable.html) class for asynchronous saving and loading.

## Installation

1. Copy the git URL of this repository.
2. In Unity, open the Package Manager from the menu by going to `Window > Package Manager`
3. Click the plus icon in the upper left and choose `Add package from git URL...`
4. Paste the git URL into the text field and click the `Add` button.

## What's Included

### DataManager API

Still WIP! More documentation will go here once the dust settles.

### Asynchronous Saving and Loading

Async methods are working, but JSON writing is still not great. In progress!

### Data Migrations

Not yet started, but planned!
  
