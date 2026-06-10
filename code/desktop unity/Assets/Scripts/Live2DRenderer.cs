using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.Physics;
using UnityEngine;

/// <summary>
/// Live2D 渲染器 — 用 Cubism SDK 渲染符玄 Live2D 模型
///
/// 使用方式：
/// 1. 在 Unity 中导入 Cubism SDK 后，模型文件会自动生成 Prefab
/// 2. 将生成的 Prefab 拖到 modelPrefab 槽
/// 3. 运行时自动实例化并跟随物理坐标
/// </summary>
[RequireComponent(typeof(DesktopPet))]
public class Live2DRenderer : MonoBehaviour, IPetRenderer
{
    // ================ 可调参数（改这里）================
    const float BREATH_AMPLITUDE   = 0.5f;    // 呼吸幅度
    const float BODY_SWAY_X        = 2.0f;  // 身体左右摆
    const float BODY_SWAY_Y        = 0.5f;  // 身体上下晃
    const float BODY_SWAY_Z        = 0.4f;  // 身体旋转
    const float HEAD_X             = 0.6f;  // 头部左右
    const float HEAD_Y             = 0.4f;  // 头部上下
    const float EYE_X              = 3f;    // 眼珠左右
    const float EYE_Y              = 2f;    // 眼珠上下
    const float IDLE_TILT          = 8f;    // 歪头幅度
    const float IDLE_SMILE         = 0.6f;  // 微笑幅度
    const float IDLE_MOUTH         = 0.4f;  // 张嘴幅度
    const float IDLE_BROW          = 6f;    // 眉毛幅度（动作2）
    const float IDLE_BROW_Y        = 6f;    // 眉毛抬起幅度（动作3）
    // ==================================================
    [Header("模型 Prefab")]
    [Tooltip("Cubism SDK 导入后生成的模型 Prefab（拖拽到这里）")]
    public GameObject modelPrefab;

    [Header("显示设置")]
    [Tooltip("模型缩放")]
    public float modelScale = 200f;

    [Tooltip("模型垂直偏移（像素）")]
    public float verticalOffset = 50f;

    // Cubism 组件
    private GameObject _modelRoot;
    private CubismModel _cubismModel;
    private CubismPhysicsController _physicsController;

    // DesktopPet 引用
    private DesktopPet _pet;

    // 姿势锁定
    private bool _poseLocked = false;
    private float _poseLockUntil = 0f;

    // 眨眼
    private float _blinkTime = 0f;
    private float _blinkInterval = 3f;
    private bool _isBlinking = false;
    private float _blinkPhase = 0f;

    // 呼吸
    private float _breathPhase = 0f;

    // 随机小动作（站立时触发）
    private float _idleActionTime = 0f;
    private float _idleActionInterval = 8f;
    private int _currentIdleAction = 0; // 0=无, 1=歪头, 2=微笑, 3=挑眉

    // 随机微动用噪声偏移
    private float _noiseTimeX = 0f;
    private float _noiseTimeY = 0f;

    // 是否已加载
    private bool _loaded = false;

    private void Start()
    {
        _pet = GetComponent<DesktopPet>();

        // 注意：已移除 modelScale 的自动重置逻辑，允许用户在 Inspector 中设置任意值
        TryLoadModel();
    }

    private void TryLoadModel()
    {
        if (modelPrefab != null)
        {
            _modelRoot = Instantiate(modelPrefab, transform);
            _cubismModel = _modelRoot.GetComponentInChildren<CubismModel>();
            _physicsController = _modelRoot.GetComponentInChildren<CubismPhysicsController>();
            
            // 自动启用所有 Renderer（符玄模型需要）
            var renderers = _modelRoot.GetComponentsInChildren<Renderer>();
            int enabledCount = 0;
            foreach (var renderer in renderers) {
                if (!renderer.enabled) {
                    renderer.enabled = true;
                    enabledCount++;
                }
            }
            if (enabledCount > 0) {
                Debug.Log($"[Live2DRenderer] 自动启用了 {enabledCount} 个 Renderer");
            }
            
            _loaded = true;
            Debug.Log($"[Live2DRenderer] 模型 Prefab 实例化完成, CubismModel={_cubismModel != null}, Physics={_physicsController != null}");
            if (_cubismModel != null)
                Debug.Log($"[Live2DRenderer] 参数数量: {_cubismModel.Parameters.Length}");
        }

        if (!_loaded)
        {
            Debug.LogError("[Live2DRenderer] 没有设置 modelPrefab，请在 Inspector 中拖拽 Cubism 导入的模型 Prefab");
            enabled = false;
        }
    }

    private void Update()
    {
        if (!_loaded || _cubismModel == null) return;

        UpdateModelPosition();
    }

    private void LateUpdate()
    {
        if (!_loaded || _cubismModel == null) return;

        // 累积噪声时间
        _noiseTimeX += Time.deltaTime * 0.6f;
        _noiseTimeY += Time.deltaTime * 0.4f;
        _breathPhase += Time.deltaTime * 2.0f;

        UpdateBlink();
        UpdateIdleAnimation();
    }

    /// <summary>
    /// 核心空闲动画 — 使用 Perlin 噪声实现自然微动
    /// 只影响呼吸和极轻微的身体晃动，没有明显的周期性上下
    /// </summary>
    private void UpdateIdleAnimation()
    {
        // === 呼吸（为物理提供驱动信号，使衣服手臂自然摆动）===
        float breath = (Mathf.PerlinNoise(_breathPhase, 0f) - 0.5f) * BREATH_AMPLITUDE;
        SetParameter("ParamBreath", breath);

        // === 身体晃动（为物理提供输入，幅度轻微不显眼）===
        float swayX = (Mathf.PerlinNoise(_noiseTimeX, 1f) - 0.5f) * BODY_SWAY_X;
        float swayY = (Mathf.PerlinNoise(_noiseTimeX, 2f) - 0.5f) * BODY_SWAY_Y;
        float swayZ = (Mathf.PerlinNoise(_noiseTimeX, 3f) - 0.5f) * BODY_SWAY_Z;
        SetParameter("ParamBodyAngleX", swayX);
        SetParameter("ParamBodyAngleY", swayY);
        SetParameter("ParamBodyAngleZ", swayZ);

        // === 头部微动 ===
        float headX = (Mathf.PerlinNoise(_noiseTimeX, 4f) - 0.5f) * HEAD_X;
        float headY = (Mathf.PerlinNoise(_noiseTimeX, 5f) - 0.5f) * HEAD_Y;
        SetParameter("ParamAngleX", headX);
        SetParameter("ParamAngleY", headY);

        // === 眼球（Perlin 噪声平滑变化，不突兀）===
        float eyeX = (Mathf.PerlinNoise(_noiseTimeY, 6f) - 0.5f) * EYE_X;
        float eyeY = (Mathf.PerlinNoise(_noiseTimeY, 7f) - 0.5f) * EYE_Y;
        SetParameter("ParamEyeBallX", eyeX);
        SetParameter("ParamEyeBallY", eyeY);

        // === 随机小动作（隔一段时间触发一次）===
        _idleActionTime += Time.deltaTime;
        if (_idleActionTime >= _idleActionInterval)
        {
            _idleActionTime = 0f;
            _currentIdleAction = Random.Range(1, 4);
            _idleActionInterval = Random.Range(8f, 18f);
        }

        if (_currentIdleAction > 0)
        {
            _idleActionTime = Mathf.Min(_idleActionTime, 1.5f);
            float t = Mathf.Sin(_idleActionTime / 1.5f * Mathf.PI);

            if (_currentIdleAction == 1)
            {
                // 歪头
                float tilt = t * IDLE_TILT;
                SetParameter("ParamAngleZ", tilt);
            }
            else if (_currentIdleAction == 2)
            {
                // 微笑眯眼
                SetParameter("ParamEyeLSmile", t * IDLE_SMILE);
                SetParameter("ParamEyeRSmile", t * IDLE_SMILE);
                SetParameter("ParamMouthForm", t * IDLE_MOUTH);
            }
            else if (_currentIdleAction == 3)
            {
                // 眉毛微动
                SetParameter("ParamBrowRY", t * IDLE_BROW_Y);
                SetParameter("ParamBrowLY", t * IDLE_BROW_Y);
            }

            if (_idleActionTime >= 1.5f)
            {
                // 重置
                _currentIdleAction = 0;
                _idleActionTime = 0f;
                SetParameter("ParamAngleZ", 0f);
                SetParameter("ParamEyeLSmile", 0f);
                SetParameter("ParamEyeRSmile", 0f);
                SetParameter("ParamMouthForm", 0f);
                SetParameter("ParamBrowRY", 0f);
                SetParameter("ParamBrowLY", 0f);
            }
        }
    }

    /// <summary>
    /// 将屏幕坐标转为世界坐标，定位模型
    /// </summary>
    private void UpdateModelPosition()
    {
        if (_modelRoot == null) return;

        Camera cam = Camera.main;
        if (cam == null) return;

        // 屏幕坐标 (左上原点, Y向下) → Unity 世界坐标
        float worldX = _pet.petX + _pet.petWidth / 2f;
        float worldY = _pet.petY + _pet.petHeight / 2f + verticalOffset;

        Vector3 screenPos = new Vector3(worldX, Screen.height - worldY, 10f);
        Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);

        _modelRoot.transform.position = worldPos;

        // 根据朝向翻转（直接设置scale，不基于当前值）
        bool faceRight = _pet.petVx >= 0;
        Vector3 scale = new Vector3(modelScale, modelScale, 1f);
        scale.x *= (faceRight ? 1 : -1);
        _modelRoot.transform.localScale = scale;
    }

    /// <summary>
    /// 自动眨眼
    /// </summary>
    private void UpdateBlink()
    {
        if (_isBlinking)
        {
            _blinkPhase += Time.deltaTime;
            float blinkValue = Mathf.Clamp01(Mathf.Abs(Mathf.Sin(_blinkPhase * 20f)));
            SetParameter("ParamEyeLOpen", blinkValue);
            SetParameter("ParamEyeROpen", blinkValue);

            if (_blinkPhase >= 0.15f)
            {
                _isBlinking = false;
                _blinkPhase = 0f;
                SetParameter("ParamEyeLOpen", 1f);
                SetParameter("ParamEyeROpen", 1f);
            }
        }
        else
        {
            _blinkTime += Time.deltaTime;
            if (_blinkTime >= _blinkInterval)
            {
                _blinkTime = 0f;
                _isBlinking = true;
                _blinkInterval = Random.Range(2f, 5f);
            }
        }
    }

    private void SetParameter(string name, float value)
    {
        if (_cubismModel == null) return;
        var param = _cubismModel.Parameters.FindById(name);
        if (param != null) param.Value = value;
    }

    #region IPetRenderer

    public void ShowDragPose()
    {
        SetParameter("ParamBodyAngleX", 0f);
        SetParameter("ParamAngleX", 0f);
        _poseLocked = false;
    }

    public void ShowClickPose()
    {
        SetParameter("ParamEyeLOpen", 0.3f);
        SetParameter("ParamEyeROpen", 0.3f);
        SetParameter("ParamAngleX", 8f);
        SetParameter("ParamBodyAngleX", -5f);
        _poseLocked = true;
        _poseLockUntil = Time.time + 1.0f;
    }

    public void ShowLandPose()
    {
        SetParameter("ParamBodyAngleX", 0f);
        _poseLocked = false;
    }

    public void ShowWalkPose()
    {
        if (_poseLocked && Time.time < _poseLockUntil) return;
        _poseLocked = false;
    }

    public void ShowStopPose(float lockSeconds)
    {
        if (lockSeconds > 0f)
        {
            _poseLocked = true;
            _poseLockUntil = Time.time + lockSeconds;
        }
    }

    public void OnPetUpdate(int petX, int petY, int petWidth, int petHeight,
                            int petVx, int petVy, bool onGround, bool isDragging, bool isPaused)
    {
        if (!_loaded || _cubismModel == null) return;

        if (isDragging) return;

        if (_poseLocked && Time.time < _poseLockUntil) return;
        _poseLocked = false;

        if (!onGround && petVy > 0)
        {
            // 下落 — 身体前倾（不调用站立时的UpdateBreath等）
            SetParameter("ParamBodyAngleX", -3f);
            SetParameter("ParamAngleX", -5f);
            SetParameter("ParamBreath", 0f);
        }
        else if (onGround)
        {
            if (petVx != 0)
            {
                // 行走 — 身体有节奏摆动 + 头发飘动感
                float sway = Mathf.Sin(Time.time * 8f) * 2.5f;
                float bob = Mathf.Sin(Time.time * 16f) * 1.0f;
                SetParameter("ParamBodyAngleX", sway);
                SetParameter("ParamAngleX", sway * 0.6f);
                SetParameter("ParamBodyAngleY", bob);
                SetParameter("ParamBreath", Mathf.Abs(Mathf.Sin(Time.time * 8f)) * 2f);
            }
            // 站立 — 完全交给 Update 中的 UpdateBreath/UpdateEyeMovement/UpdateIdleActions
        }
    }

    #endregion
}
