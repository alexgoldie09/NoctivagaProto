using UnityEditor;
using UnityEditor.Tilemaps;
using UnityEngine;
using UnityEngine.Tilemaps;

/// <summary>
/// Custom inspector for GameTileInfoBrush that displays tile asset details
/// and custom metadata if the tile is a GameTile.
/// </summary>
[CustomEditor(typeof(GameTileInfoBrush))]
public class GameTileInfoBrushEditor : GridBrushEditor
{
    private GameTileInfoBrush _brush;

    /// <summary>
    /// Called when the editor is enabled.
    /// </summary>
    protected override void OnEnable()
    {
        base.OnEnable();
        _brush = target as GameTileInfoBrush;
    }

    /// <summary>
    /// Draw the custom inspector UI for the brush.
    /// </summary>
    public override void OnInspectorGUI()
    {
        // Draw the default GridBrush UI first (size, pivot, etc.)
        base.OnInspectorGUI();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("GameTile Inspector", EditorStyles.boldLabel);

        if (_brush == null)
        {
            EditorGUILayout.HelpBox("Brush reference is null.", MessageType.Warning);
            return;
        }

        TileBase pickedTile = _brush.LastPickedTile;

        if (pickedTile == null)
        {
            EditorGUILayout.HelpBox("No tile picked yet. Use the Pick tool on the Tile Palette or a Tilemap.", MessageType.Info);
            return;
        }

        using (new EditorGUI.DisabledScope(true))
        {
            EditorGUILayout.ObjectField("Picked Tile", pickedTile, typeof(TileBase), false);
            EditorGUILayout.Vector3IntField("Picked Cell", _brush.LastPickedCell);
            EditorGUILayout.TextField("Asset Path", _brush.LastPickedAssetPath);
        }

        EditorGUILayout.Space(4);

        using (new EditorGUILayout.HorizontalScope())
        {
            if (GUILayout.Button("Ping Source Asset"))
            {
                EditorGUIUtility.PingObject(pickedTile);
                Selection.activeObject = pickedTile;
            }

            if (GUILayout.Button("Select Source Asset"))
            {
                Selection.activeObject = pickedTile;
            }
        }

        EditorGUILayout.Space(8);

        // Show generic Tile fields if it's a normal Tile
        if (pickedTile is Tile standardTile)
        {
            EditorGUILayout.LabelField("Base Tile Data", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.ObjectField("Sprite", standardTile.sprite, typeof(Sprite), false);
                EditorGUILayout.ObjectField("Color", null, typeof(Object), false); // placeholder (optional)
                EditorGUILayout.EnumPopup("Collider Type", standardTile.colliderType);
            }
        }

        // Show GameTile-specific metadata
        if (pickedTile is GameTile gameTile)
        {
            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("GameTile Metadata", EditorStyles.boldLabel);

            using (new EditorGUI.DisabledScope(true))
            {
                EditorGUILayout.EnumPopup("Kind", gameTile.kind);
                EditorGUILayout.Toggle("Walkable By Default", gameTile.walkableByDefault);
                EditorGUILayout.EnumPopup("Enter Effect", gameTile.enterEffect);

                if (gameTile.kind == TileKind.Gate)
                {
                    EditorGUILayout.TextField("Gate Key ID", gameTile.gateKeyID);
                    EditorGUILayout.Toggle("Consumes Key", gameTile.consumesKey);
                }
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Picked tile is not a GameTile. It may be a standard Tile or another TileBase type.", MessageType.None);
        }
    }
}