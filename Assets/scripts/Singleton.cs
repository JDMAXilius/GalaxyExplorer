// Copyright (c) Microsoft Corporation. All rights reserved.
// Licensed under the MIT License. See LICENSE in the project root for license information.

using UnityEngine;

/// <summary>
/// A component with one instance; its root GameObject is marked <see cref="Object.DontDestroyOnLoad"/>.
/// </summary>
public class Singleton<T> : MonoBehaviour where T : Singleton<T>
{
    private static T instance;

    /// <summary>
    /// Searches the scene once if Awake has not registered an instance yet; more than one found logs an error and returns null.
    /// </summary>
    public static T Instance
    {
        get
        {
            if (!IsInitialized && searchForInstance)
            {
                searchForInstance = false;
                T[] objects = FindObjectsByType<T>(FindObjectsSortMode.None);
                if (objects.Length == 1)
                {
                    instance = objects[0];
                    instance.gameObject.GetParentRoot().DontDestroyOnLoad();
                }
                else if (objects.Length > 1)
                {
                    Debug.LogErrorFormat("Expected exactly 1 {0} but found {1}.", typeof(T).Name, objects.Length);
                }
            }
            return instance;
        }
    }

    private static bool searchForInstance = true;

    public static void AssertIsInitialized()
    {
        Debug.Assert(IsInitialized, string.Format("The {0} singleton has not been initialized.", typeof(T).Name));
    }

    public static bool IsInitialized
    {
        get
        {
            return instance != null;
        }
    }

    /// <summary>Safe from Awake and OnEnable, where the instance may not have registered itself yet.</summary>
    public static bool ConfirmInitialized()
    {
        T access = Instance;
        return IsInitialized;
    }

    /// <summary>Subclasses must call base.Awake(), or duplicates survive and Instance falls back to a one-off scene search.</summary>
    protected virtual void Awake()
    {
        if (IsInitialized && instance != this)
        {
            if (Application.isEditor)
            {
                DestroyImmediate(this);
            }
            else
            {
                Destroy(this);
            }

            Debug.LogErrorFormat("Trying to instantiate a second instance of singleton class {0}. Additional Instance was destroyed", GetType().Name);
        }
        else if (!IsInitialized)
        {
            instance = (T)this;
            searchForInstance = false;
            gameObject.GetParentRoot().DontDestroyOnLoad();
        }
    }

    /// <summary>Subclasses must call base.OnDestroy(), or Instance never searches for a replacement.</summary>
    protected virtual void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
            searchForInstance = true;
        }
    }
}