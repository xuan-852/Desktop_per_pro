using UnityEngine;

/// <summary>
/// 3D 模型渲染器 — 用 Unity Animator 控制符玄 3D 模型
/// 负责走路、飞行等需要骨骼动画的动作
///
/// ⚠️ 启动时自动设置：
///   - Main Camera → Orthographic（自动计算 Size）
///   - 添加 Directional Light（如果没有）
///   - 背景保持绿色 #00FF00 用于 Color Key 抠图
/// </summary>
[RequireComponent(typeof(DesktopPet))]
public class Model3DRenderer : MonoBehaviour, IPetRenderer
{
    // ===================================================================
    // ===== 调参区 — 你只需要改这里的数值 =====
    // ===================================================================

    [Header("模型 Prefab")]
    [Tooltip("FuXuan.fbx 生成的预制体（拖拽到这里）")]
    public GameObject modelPrefab;

    [Header("🔧 显示调整")]
    [Tooltip("模型整体大小 | 太大→改小 | 太小→改大")]
    public float modelScale = 0.01f;

    [Tooltip("模型上下位置偏移（像素，正=下移，负=上移）")]
    public float verticalOffset = 50f;

    [Tooltip("相机远近：越小模型显得越大，越大模型显得越小")]
    public float orthoSize = 5f;

    // ===================================================================
    // ===== 下面的不用改 =====
    // ===================================================================

    private GameObject _modelRoot;
    private Animator _animator;
    private DesktopPet _pet;

    // Animator 参数 Hash
    private static readonly int SpeedParam = Animator.StringToHash("Speed");

    private bool _loaded = false;

    private void Start()
    {
        _pet = GetComponent<DesktopPet>();
        SetupCameraAndLighting();
        TryLoadModel();
    }

    /// <summary>
    /// 自动配置相机为 Orthographic + 添加灯光 + 保持绿色背景
    /// </summary>
    private void SetupCameraAndLighting()
    {
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogError("[Model3DRenderer] 找不到 Main Camera");
            return;
        }

        // 1. 强制 Orthographic
        cam.orthographic = true;
        cam.orthographicSize = orthoSize;

        // 2. 保持绿色背景（Color Key 用）
        cam.backgroundColor = new Color(0f, 1f, 0f, 1f);

        // 3. 清除标记设为纯色（不画天空盒）
        cam.clearFlags = CameraClearFlags.SolidColor;

        // 4. 近远裁剪面
        cam.nearClipPlane = 0.1f;
        cam.farClipPlane = 100f;

        // 5. 相机位置（Z 负方向看向场景）
        cam.transform.position = new Vector3(0, 0, -10f);
        cam.transform.rotation = Quaternion.identity;

        Debug.Log($"[Model3DRenderer] 相机已设为 Orthographic, Size={orthoSize}");

        // 6. 如果没有 Directional Light，自动添加一个
        Light existingLight = FindObjectOfType<Light>();
        if (existingLight == null)
        {
            GameObject lightGO = new GameObject("Directional Light (Auto)");
            Light light = lightGO.AddComponent<Light>();
            light.type = LightType.Directional;
            light.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
            light.intensity = 1.2f;
            Debug.Log("[Model3DRenderer] 已自动添加 Directional Light");
        }
    }

    private void TryLoadModel()
    {
        Debug.Log($"[Model3DRenderer] TryLoadModel called, modelPrefab={(modelPrefab != null ? modelPrefab.name : "NULL")}");

        if (modelPrefab != null)
        {
            _modelRoot = Instantiate(modelPrefab, transform);
            Debug.Log($"[Model3DRenderer] Model instantiated: {_modelRoot.name}, Position: {_modelRoot.transform.position}");

            _animator = _modelRoot.GetComponentInChildren<Animator>();
            Debug.Log($"[Model3DRenderer] Animator found: {_animator != null}");

            var renderers = _modelRoot.GetComponentsInChildren<Renderer>(true);
            Debug.Log($"[Model3DRenderer] Renderers count: {renderers.Length}");

            _loaded = true;
            Debug.Log($"[Model3DRenderer] 模型实例化完成, Animator={_animator != null}");
        }

        if (!_loaded)
        {
            Debug.LogError("[Model3DRenderer] 没有设置 modelPrefab");
            enabled = false;
        }
    }

    private void Update()
    {
        if (!_loaded || _modelRoot == null) return;
        UpdateModelPosition();
    }

    /// <summary>
    /// 屏幕像素坐标 → Unity 世界坐标（Orthographic 专用）
    /// </summary>
    private void UpdateModelPosition()
    {
        if (_modelRoot == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // 左上原点 Y向下 → Unity 屏幕坐标 Y向上
        float centerX = _pet.petX + _pet.petWidth / 2f;
        float centerY = _pet.petY + _pet.petHeight / 2f + verticalOffset;

        // Orthographic 下 Z 值不重要，统一用 0
        Vector3 screenPos = new Vector3(centerX, Screen.height - centerY, 0f);
        Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);

        // Z 稍微往前推，确保在相机前面且不被其他物体遮挡
        worldPos.z = 0f;

        _modelRoot.transform.position = worldPos;

        // 根据朝向翻转
        bool faceRight = _pet.petVx >= 0;
        Vector3 scale = new Vector3(modelScale, modelScale, modelScale);
        scale.x *= (faceRight ? 1 : -1);
        _modelRoot.transform.localScale = scale;
    }

    /// <summary>
    /// 设置 Animator 的 Speed 参数，控制 Idle ↔ Walk 过渡
    /// </summary>
    private void SetSpeed(float speed)
    {
        if (_animator != null && _animator.isActiveAndEnabled)
            _animator.SetFloat(SpeedParam, speed);
    }

    /// <summary>
    /// 给 HybridRenderer 用的：获取模型根物体
    /// </summary>
    public GameObject GetModelRoot()
    {
        return _modelRoot;
    }

    /// <summary>
    /// 设置模型所有 Renderer 的透明度（HybridRenderer 交叉淡入淡出用）
    /// </summary>
    public void SetAlpha(float alpha)
    {
        if (_modelRoot == null) return;
        var renderers = _modelRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            Color c = r.material.color;
            c.a = Mathf.Clamp01(alpha);
            r.material.color = c;
        }
    }

    #region IPetRenderer

    public void ShowDragPose()
    {
        // 拖拽时保持 Idle
        SetSpeed(0f);
    }

    public void ShowClickPose()
    {
        SetSpeed(0f);
    }

    public void ShowLandPose()
    {
        SetSpeed(0f);
    }

    public void ShowWalkPose()
    {
        SetSpeed(1f);
    }

    public void ShowStopPose(float lockSeconds)
    {
        SetSpeed(0f);
    }

    public void OnPetUpdate(int petX, int petY, int petWidth, int petHeight,
                            int petVx, int petVy, bool onGround, bool isDragging, bool isPaused)
    {
        if (!_loaded || _animator == null) return;

        if (isDragging) return;

        if (onGround)
        {
            // 地面：有水平速度就走路，否则待机
            SetSpeed(Mathf.Abs(petVx) > 0 ? 1f : 0f);
        }
        else
        {
            // 空中：待机（下落姿态后续添加）
            SetSpeed(0f);
        }
    }

    #endregion
}
