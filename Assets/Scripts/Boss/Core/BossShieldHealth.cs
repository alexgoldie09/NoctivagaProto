using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Optional boss shield health component with its own HP and damage flash.
/// Can define shield-protected grid cells using marker transforms.
/// </summary>
public class BossShieldHealth : MonoBehaviour
{
    [Header("Shield Health")]
    [Min(1)]
    [SerializeField] private int maxHP = 5;
    [Min(0)]
    [SerializeField] private int startingHP = 5;

    [Header("Shield Visuals")]
    [SerializeField] private SpriteRenderer shieldRenderer;
    [SerializeField] private Animator shieldAnimator;
    [SerializeField] private Color flashColor = Color.white;
    [Min(0f)]
    [SerializeField] private float flashTotalDuration = 0.5f;
    [Min(0.01f)]
    [SerializeField] private float flashInterval = 0.1f;

    [Header("Shield Cells")]
    [Tooltip("Transform whose position/scale defines the shielded area.")]
    [SerializeField] private Transform shieldAreaTransform;
    [SerializeField] private TilemapGridManager grid;
    [SerializeField] private bool drawGizmos = true;
    [SerializeField] private bool drawLabels = true;

    private readonly List<Vector3Int> shieldCells = new();
    private Coroutine flashRoutine;
    private Color initialColor = Color.white;
    private bool shieldHit;

    public event Action<int, int> OnShieldChanged;
    public event Action OnShieldBroken;

    public int MaxHP => maxHP;
    public int CurrentHP { get; private set; }
    
    public Animator ShieldAnimator => shieldAnimator;
    public bool IsBroken => CurrentHP <= 0;
    public bool ShieldHit => shieldHit;
    public void ClearShieldHit() => shieldHit = false;

    private void Awake()
    {
        if (shieldRenderer == null)
            shieldRenderer = GetComponent<SpriteRenderer>();
        
        if (shieldAnimator == null)
            shieldAnimator = GetComponent<Animator>();

        if (shieldRenderer != null)
            initialColor = shieldRenderer.color;

        if (startingHP <= 0)
            startingHP = maxHP;

        CurrentHP = Mathf.Clamp(startingHP, 0, maxHP);
        RebuildShieldCells();

        OnShieldChanged?.Invoke(CurrentHP, maxHP);
    }

    private void OnEnable()
    {
        RebuildShieldCells();
    }

    /// <summary>
    /// Applies damage to the shield. Returns true if damage was applied.
    /// </summary>
    public bool ApplyDamage(int amount)
    {
        if (amount <= 0 || IsBroken)
            return false;

        int prev = CurrentHP;
        CurrentHP = Mathf.Max(0, CurrentHP - amount);

        if (CurrentHP != prev)
        {
            OnShieldChanged?.Invoke(CurrentHP, maxHP);
            Flash();
            shieldHit = true;

            if (CurrentHP == 0)
                OnShieldBroken?.Invoke();
        }

        return true;
    }

    /// <summary>
    /// Returns true if the cell is covered by the shield markers.
    /// </summary>
    public bool IsCellShielded(Vector3Int cell)
    {
        RebuildShieldCells();
        return shieldCells.Contains(cell);
    }

    /// <summary>
    /// Restores shield HP to max (optional for reuse).
    /// </summary>
    public void ResetShield()
    {
        CurrentHP = maxHP;
        OnShieldChanged?.Invoke(CurrentHP, maxHP);
    }

    private void Flash()
    {
        if (shieldRenderer == null || flashTotalDuration <= 0f || flashInterval <= 0f)
            return;

        if (flashRoutine != null)
            StopCoroutine(flashRoutine);

        flashRoutine = StartCoroutine(FlashRoutine());
    }

    private IEnumerator FlashRoutine()
    {
        if (shieldRenderer == null)
            yield break;

        float elapsed = 0f;
        bool useFlash = true;

        while (elapsed < flashTotalDuration)
        {
            shieldRenderer.color = useFlash ? flashColor : initialColor;
            useFlash = !useFlash;

            float step = Mathf.Min(flashInterval, flashTotalDuration - elapsed);
            elapsed += step;
            yield return new WaitForSeconds(step);
        }

        shieldRenderer.color = initialColor;
    }

    private void RebuildShieldCells()
    {
        shieldCells.Clear();

        if (grid == null)
            grid = TilemapGridManager.Instance;
        
        if (grid == null)
            return;

        if (shieldAreaTransform != null)
            AddCellsFromBounds(shieldCells, GetTransformBounds(shieldAreaTransform), grid);
    }

    private static Bounds GetTransformBounds(Transform source)
    {
        Vector3 size = source.lossyScale;
        size.x = Mathf.Abs(size.x);
        size.y = Mathf.Abs(size.y);
        size.z = 0.01f;
        return new Bounds(source.position, size);
    }

    private static void AddCellsFromBounds(List<Vector3Int> cells, Bounds bounds, TilemapGridManager grid)
    {
        const float epsilon = 0.001f;
        Vector3Int minCell = grid.WorldToCell(bounds.min);
        Vector3Int maxCell = grid.WorldToCell(bounds.max - new Vector3(epsilon, epsilon, 0f));

        for (int x = minCell.x; x <= maxCell.x; x++)
        {
            for (int y = minCell.y; y <= maxCell.y; y++)
            {
                var cell = new Vector3Int(x, y, 0);
                if (!cells.Contains(cell))
                    cells.Add(cell);
            }
        }
    }

#if UNITY_EDITOR
    /// <summary>
    /// Draws gizmos for target cells and optional labels in the editor.
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        if (!drawGizmos) 
            return;

        if (grid == null)
            grid = TilemapGridManager.Instance;
        
        if (grid == null)
            grid = FindFirstObjectByType<TilemapGridManager>();

        if (grid == null)
            return;

        // Always rebuild in editor so gizmos match moved markers
        RebuildShieldCells();

        Gizmos.matrix = Matrix4x4.identity;
        Gizmos.color = Color.black;

        foreach (var cell in shieldCells)
        {
            Vector3 center = grid.CellToWorldCenter(cell);
            Vector3 size = Vector3.one * 0.9f;

            Gizmos.DrawWireCube(center, size);

            if (drawLabels)
                Handles.Label(center + Vector3.up * 0.3f, $"{cell.x},{cell.y}");
        }
    }
#endif
}
