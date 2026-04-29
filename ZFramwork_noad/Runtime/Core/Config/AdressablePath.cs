using System.IO;
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(menuName = "FrameworkAsset/Create AdressablePath")]
[Serializable]
public class AdressablePath : ScriptableObject
{
    private static AdressablePath _instance;

    public string audio_path;
    public string material_path;
    public string prefab_game_path;
    public string prefab_ui_path;
    public string scene_path;
    public string sprite_game_path;
    public string sprite_ui_path;
    public string texture_game_path;
    public string texture_ui_path;

    public List<string> commonAudioPaths;

    public static AdressablePath Instance { get {
            if (_instance == null)
            {
                _instance = Resources.Load<AdressablePath>("AdressablePath");
            }
            return _instance;
        }
    }
}

