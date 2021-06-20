using System.Collections.Generic;
using System.Linq;
using System.Xml;
using UnityEditor;
using UnityEngine;

/// <summary>
/// Scripting Define Symbols を設定するウィンドウ
/// </summary>
public class DefineSymbols : EditorWindow
{
    const string ITEM_NAME    = "Tools/Define Symbols";      // コマンド名
    const string WINDOW_TITLE = "Define Symbols";            // ウィンドウのタイトル

    [System.Serializable]
    public class DataParameter
    {
        /// <summary></summary>
        public bool				Enabled;
        /// <summary></summary>
        public string			Define;
        /// <summary></summary>
        public string			Description;
        /// <summary></summary>
        public BuildTargetGroup	Target;
        
        /// <summary></summary>
        public DataParameter(bool enabled, string define, string description, BuildTargetGroup target)
        {
            Enabled     = enabled;
            Define	    = define;
            Description = description;
            Target	    = target;
        }
    }
    
    [System.Serializable]
    public class JsonParameter
    {
        public List<DataParameter>	Data = new List<DataParameter>();
    }

    static Vector2			curretScroll  = Vector2.zero;
    static string			defineSymbols = null;
    static string			jsonfile      = null;
    static JsonParameter	defines;
    
    /// <summary>
    /// ウィンドウを開きます
    /// </summary>
    [MenuItem(ITEM_NAME)]
    static void MenuExec()
    {
        if (Init() == true)
        {
            var window = GetWindow<DefineSymbols>(true, WINDOW_TITLE);
            window.Show();
        }
    }

    /// <summary>
    /// ウィンドウオープンの可否を取得します
    /// </summary>
    [MenuItem(ITEM_NAME, true)]
    static bool CanCreate()
    {
        bool enable = !EditorApplication.isPlaying && !Application.isPlaying && !EditorApplication.isCompiling;
        if (enable == false)
        {
            Debug.Log ($"{WINDOW_TITLE}: can't create. wait seconds.");
        }
        return enable;
    }

    /// <summary>
    /// 初期化
    /// </summary>
    public static bool Init()
    {
        // クラス名を取得
        string className = nameof(DefineSymbols);
        
        // クラス名.cs というファイルを検索
        string[]	files = System.IO.Directory.GetFiles(Application.dataPath, className + ".cs", System.IO.SearchOption.AllDirectories);
        string		file  = null;
        if (files.Length == 1)
        {
            // 同じパスに クラス名.json としてデータを格納
            file = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(files[0]), className + ".json");
            
            jsonfile = file;
        }
        
        defines = null;
        
        // クラス名.json があればそのデータを読み込む
        if (System.IO.File.Exists(jsonfile) == true)
        {
            string data = null;
            
            using (System.IO.StreamReader sr = new System.IO.StreamReader(jsonfile))
            {
                data = sr.ReadToEnd();
            }
            defines = JsonUtility.FromJson<JsonParameter>(data);
        }
        
        // データがなければ Scripting Define Symbols からデータを作成
        defineSymbols = PlayerSettings.GetScriptingDefineSymbolsForGroup(EditorUserBuildSettings.selectedBuildTargetGroup);
        if (string.IsNullOrEmpty(defineSymbols) == false && defineSymbols[defineSymbols.Length-1] != ';')
        {
            defineSymbols += ";";
        }

        List<string> symbols =
            defineSymbols
            .Split(';')
            .Select(x => x.Trim())
            .ToList();

        if (defines == null)
        {
            // json 新規作成
            defines = new JsonParameter();
            
            symbols.ForEach(
                s =>
                {
                    if (string.IsNullOrEmpty(s) == true) return;

                    defines.Data.Add(new DataParameter(true, s, "description: " + s, BuildTargetGroup.Unknown));
                }
            );
        }
        else
        {
            
            // DefineSymbols にあって、json にないものを登録
            symbols.ForEach(
                s =>
                {
                    if (string.IsNullOrEmpty(s) == true) return;

                    if (defines.Data.FindAll(d => d.Define == s).IsNullOrEmpty() == true)
                    {
                        defines.Data.Add(new DataParameter(true, s, "description: " + s, BuildTargetGroup.Unknown));
                    }
                }
            );
            
            // 逆に、json にあって DefineSymbols にないものを登録
            string newSymbol = getScriptingDefineSymbols(defineSymbols);
            if (newSymbol != defineSymbols)
            {
                defineSymbols = newSymbol;
                refreshScriptingDefineSymbols();

                EditorUtility.DisplayDialog($"Notice", "Auto-Update ScriptingDefineSymbols And Compiling. Please 'Reopen'.", "ok");
                return false;
            }
        }

        return true;
    }

    /// <summary>
    /// GUI を表示する時に呼び出されます
    /// </summary>
    void OnGUI()
    {
        if (defines == null)
        {
            Close();
            return;
        }
        
        GUILayout.Label ("Scripting Define Symbols", EditorStyles.boldLabel);
        
        // Scripting Define Symbols
        EditorGUILayout.SelectableLabel(defineSymbols, EditorStyles.textField, GUILayout.Height(EditorGUIUtility.singleLineHeight));
        
        GUILayout.Space(20);
        
        // 設定項目
        curretScroll = EditorGUILayout.BeginScrollView(curretScroll);
        EditorGUILayout.BeginVertical("box");
        for (int i = 0; i < defines.Data.Count; i++)
        {
            var def = defines.Data[i];

            if (GUILayout.Button($"- remove (No. {i})", GUILayout.Width(120)))
            {
                defines.Data.RemoveAt(i);
            }
            def.Enabled	= EditorGUILayout.BeginToggleGroup($"enable", def.Enabled);

            GUILayout.BeginHorizontal();
            def.Define		= EditorGUILayout.TextField("define", def.Define);
            def.Target		= (BuildTargetGroup)EditorGUILayout.EnumPopup(def.Target, GUILayout.MaxWidth(100));
            GUILayout.EndHorizontal();
            def.Description	= EditorGUILayout.TextField("desc", def.Description);
            GUILayout.Space(8);
            EditorGUILayout.EndToggleGroup();
        }
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndScrollView();
        
        GUILayout.Space(10);
        if (GUILayout.Button("+ add", GUILayout.Width(80)))
        {
            defines.Data.Add(new DataParameter(true, "", "", BuildTargetGroup.Unknown));
        }
        GUILayout.Space(40);
        
        // ＋－
        using (new EditorGUILayout.HorizontalScope())
        {

            if (GUILayout.Button("Save & Update"))
            {
                // 情報として json ファイルに格納
                if (string.IsNullOrEmpty(jsonfile) == false)
                {
                    string data = JsonUtility.ToJson(defines, true);
                
                    data = data.Replace("¥r¥n", "¥n");
                    using (System.IO.StreamWriter sw = new System.IO.StreamWriter(jsonfile, false))
                    {
                        sw.Write(data);
                    }
                }
            
                // Scripting Define Symbols を作成
                defineSymbols = getScriptingDefineSymbols(defineSymbols);
                
                // Inspector に登録
                refreshScriptingDefineSymbols();
                
                // 強制的に unity database を更新
                AssetDatabase.Refresh(ImportAssetOptions.ForceUpdate);
                
                Close();
            }
        }
        GUILayout.Space(20);
    }

    /// <summary>
    /// リストから ScriptingDefineSymbols 文字列に変換
    /// </summary>
    static string getScriptingDefineSymbols(string symbols)
    {
        string s = "";

        defines.Data.ForEach(
            x =>
            {
                if (x.Define.Trim().Length == 0 || x.Enabled == false)
                {
                    // 不要な項目
                }
                else
                {
                    if (x.Enabled == true)
                    {
                        if (x.Target == BuildTargetGroup.Unknown || x.Target == EditorUserBuildSettings.selectedBuildTargetGroup)
                        {
                            s += x.Define + ";";
                        }
                    }
                }
            }
        );

        return s;
    }

    /// <summary>
    /// ScriptingDefineSymbols に登録
    /// </summary>
    static void refreshScriptingDefineSymbols()
    {
        PlayerSettings.SetScriptingDefineSymbolsForGroup(
            EditorUserBuildSettings.selectedBuildTargetGroup,
            defineSymbols
        );
    }

}

/// <summary>
/// プラットフォーム変更時に発生するイベント
/// </summary>
public class PlatformOSChange : UnityEditor.Build.IActiveBuildTargetChanged
{
    public int callbackOrder { get { return 0; } }
    
    public void OnActiveBuildTargetChanged(BuildTarget previousTarget, BuildTarget newTarget)
    {
        Debug.Log($"{nameof(DefineSymbols)}: Change Platform OS '{newTarget}'");
        DefineSymbols.Init();
    }
}

/// <summary>
/// IsNullOrEmpty
/// </summary>
static class CollectionArrayExtension
{
    public static bool IsNullOrEmpty<T>(this ICollection<T> collection)
    {
        return collection == null || collection.Count == 0;
    }
    public static bool IsNullOrEmpty<T>(this T[] array)
    {
        return array == null || array.Length == 0;
    }
}
