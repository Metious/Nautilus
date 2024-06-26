using System.Linq;
using System.Reflection;
using HarmonyLib;
using Nautilus.Patchers;
using Nautilus.Utility;
using UnityEngine;
using UnityEngine.AddressableAssets;
using UnityEngine.SceneManagement;
using Object = UnityEngine.Object;

namespace Nautilus.Extensions;

/// <summary>
/// Contains extension methods for Unity objects.
/// </summary>
public static class GameObjectExtensions
{
    /// <summary>
    /// Checks if the object exists. This method is a wrapper to allow null-coalescing operator usage by respecting Unity's object life cycle.
    /// </summary>
    /// <param name="object">the object</param>
    /// <typeparam name="T">the <see cref="UnityEngine.Object"/> type</typeparam>
    /// <returns>The object if exists, otherwise null.</returns>
    public static T Exists<T>(this T @object) where T : Object
    {
        return @object ? @object : null;
    }
    
    /// <summary>
    /// Copies the field values from the specified component to the current component.
    /// </summary>
    /// <param name="this">The current instance to copy to.</param>
    /// <param name="copyFrom">The specified instance to copy from.</param>
    /// <typeparam name="TSelfComponent">The type of this component.</typeparam>
    /// <typeparam name="TCopiedComponent">The type of the copied component.</typeparam>
    /// <returns>The current component with the correct field values.</returns>
    /// <remarks>This method only takes effect on public fields that are serializable, or non-public fields with the <see cref="SerializeField"/> attribute.</remarks>
    /// <seealso cref="AddAndCopyComponent{TSelfComponent,TCopiedComponent}"/>
    /// <seealso cref="EnsureAndCopyComponent{TSelfComponent,TCopiedComponent}"/>
    public static TSelfComponent CopyComponent<TSelfComponent, TCopiedComponent>(this TSelfComponent @this, TCopiedComponent copyFrom)
        where TSelfComponent : Component, TCopiedComponent
    {
        var ourType = @this.GetType();
        var stolenType = copyFrom.GetType();
        
        // Only copy fields that are either public, or non-public but are serializable.
        var copiedFields = stolenType.GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic)
            .Where(f => (!f.IsPublic && f.GetCustomAttribute<SerializeField>() is {}) || (f.IsPublic && !f.IsNotSerialized));

        foreach (var copiedField in copiedFields)
        {
            if (AccessTools.Field(ourType, copiedField.Name) is not {} field)
                continue;
            
            field.SetValue(@this, copiedField.GetValue(copyFrom));
        }

        return @this;
    }
    
    /// <summary>
    /// Adds a component with the <typeparamref name="TNewComponent"/> type, then copies the field values of the <typeparamref name="TCopiedComponent"/> into it.
    /// </summary>
    /// <param name="obj">The Game object to perform this action on.</param>
    /// <typeparam name="TNewComponent">The type of the new component.</typeparam>
    /// <typeparam name="TCopiedComponent">The type of the copied component.</typeparam>
    /// <returns>The new component with the correct field values.</returns>
    /// <remarks>This method only takes effect on public fields that are serializable, or non-public fields with the <see cref="SerializeField"/> attribute.</remarks>
    /// <seealso cref="CopyComponent{TSelfComponent,TCopiedComponent}"/>
    /// <seealso cref="EnsureAndCopyComponent{TSelfComponent,TCopiedComponent}"/>
    public static TNewComponent AddAndCopyComponent<TNewComponent, TCopiedComponent>(this GameObject obj)
        where TNewComponent : Component, TCopiedComponent
    {
        var origComponent = obj.GetComponent<TCopiedComponent>();
        if (origComponent == null)
        {
            InternalLogger.Warn($"Component: '{typeof(TCopiedComponent).Name}' does not exist on object '{obj.name}'.");
            return null;
        }

        return obj.AddComponent<TNewComponent>().CopyComponent(origComponent);
    }
    
    /// <summary>
    /// Ensures a component with the <typeparamref name="TNewComponent"/> type exists, then copies the field values of the <typeparamref name="TCopiedComponent"/> into it.
    /// </summary>
    /// <param name="obj">The Game object to perform this action on.</param>
    /// <typeparam name="TNewComponent">The type of the new component.</typeparam>
    /// <typeparam name="TCopiedComponent">The type of the copied component.</typeparam>
    /// <returns>The new component with the correct field values.</returns>
    /// <remarks>This method only takes effect on public fields that are serializable, or non-public fields with the <see cref="SerializeField"/> attribute.</remarks>
    /// <seealso cref="CopyComponent{TSelfComponent,TCopiedComponent}"/>
    /// <seealso cref="EnsureAndCopyComponent{TSelfComponent,TCopiedComponent}"/>
    public static TNewComponent EnsureAndCopyComponent<TNewComponent, TCopiedComponent>(this GameObject obj)
        where TNewComponent : Component, TCopiedComponent
    {
        var origComponent = obj.GetComponent<TCopiedComponent>();
        if (origComponent == null)
        {
            InternalLogger.Warn($"Component: '{typeof(TCopiedComponent).Name}' does not exist on object '{obj.name}'.");
            return null;
        }

        return obj.EnsureComponent<TNewComponent>().CopyComponent(origComponent);
    }

    /// <summary>
    /// Searches the hierarchy under this Transform recursively and returns a child Transform with the matching name if any is found.
    /// </summary>
    /// <param name="transform">The root object of the search.</param>
    /// <param name="name">The name of the object that is being searched for.</param>
    /// <returns>If found, a reference to the transform, otherwise; null.</returns>
    public static Transform SearchChild(this Transform transform, string name)
    {
        foreach (Transform child in transform)
        {
            if (child.gameObject.name == name)
                return child;

            var recursive = SearchChild(child, name);
            if (recursive != null)
                return recursive;
        }
        return null;
    }

    /// <summary>
    /// Searches the hierarchy under this GameObject recursively and returns a child GameObject with the matching name if any is found.
    /// </summary>
    /// <param name="gameObject">The root object of the search.</param>
    /// <param name="name">The name of the object that is being searched for.</param>
    /// <returns>If found, a reference to the game object, otherwise; null.</returns>
    public static GameObject SearchChild(this GameObject gameObject, string name) => SearchChild(gameObject.transform, name).Exists()?.gameObject;

    /// <summary>
    /// Checks if this game object is a proper prefab. Proper prefabs are those that are made via the Unity Editor and are .prefab formatted.
    /// </summary>
    /// <param name="gameObject">The game object to check.</param>
    /// <returns>True if this game object is a proper prefab, otherwise false.</returns>
    /// <exception cref="System.NullReferenceException"><paramref name="gameObject"/> is null.</exception>
    public static bool IsPrefab(this GameObject gameObject)
    {
        return gameObject.scene.name is null && gameObject.scene.loadingState is Scene.LoadingState.NotLoaded;
    }
    
    /// <summary>
    /// Forces the passed <see cref="AssetReferenceGameObject"/>'s RuntimeKey to always pass the IsRuntimeKeyValid check.
    /// </summary>
    /// <param name="this">The <see cref="AssetReferenceGameObject"/> to convert</param>
    /// <returns>A reference to this instance after the operation is completed.</returns>
    public static TAssetReference ForceValid<TAssetReference>(this TAssetReference @this) where TAssetReference : AssetReference
    {
        AssetReferencePatcher.AddValidKey(@this.AssetGUID);
        return @this;
    }
}