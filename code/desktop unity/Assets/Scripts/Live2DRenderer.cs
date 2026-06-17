using Live2D.Cubism.Core;
using Live2D.Cubism.Framework;
using Live2D.Cubism.Framework.Physics;
using System.Collections.Generic;
using UnityEngine;

// ===================================================================
//  调大小 → 改下面三个数值即可（改完保存，Unity 自动重编译）
// ===================================================================
//  模型缩放:     LIVE2D_SCALE      (默认 200, 改小→变小)
//  垂直偏移:     LIVE2D_OFFSET_Y   (默认 250, 正=下移, 负=上移)
//  相机远近:     (由 Camera 控制，无需在此调整)
// ===================================================================

/// <summary>
/// Live2D 渲染器 — 用 Cubism SDK 渲染符玄 Live2D 模型
///
/// 使用方式：
/// 1. 在 Unity 中导入 Cubism SDK 后，模型文件会自动生成 Prefab
/// 2. 将生成的 Prefab 拖到 modelPrefab 槽
/// 3. 运行时自动实例化并跟随物理坐标
/// </summary>
[RequireComponent(typeof(DesktopPet))]
[DefaultExecutionOrder(801)]   // CubismPhysicsController=800，我们跑在物理之后
public class Live2DRenderer : MonoBehaviour, IPetRenderer
{
    // ===================================================================
    // ===== 🎛️ 调参区 — 改这里 =====
    // ===================================================================
    const float LIVE2D_SCALE       = 56.25f; // 模型缩放（越大→模型越大）
    const float LIVE2D_OFFSET_Y    = 0f;      // 垂直偏移（正数=下移，负数=上移）
    // ===================================================================

    // ================ 动画参数 ================
    // -- 空闲待机 --
    const float BREATH_AMPLITUDE   = 0.4f;    // 呼吸幅度
    const float BODY_SWAY_X        = 10.0f;  // 身体左右摆
    const float BODY_SWAY_Y        = 0.5f;   // 身体上下晃
    const float BODY_SWAY_Z        = 0.4f;   // 身体旋转
    const float HEAD_X             = 0.6f;   // 头部左右
    const float HEAD_Y             = 0.4f;   // 头部上下
    const float EYE_X              = 3f;     // 眼珠左右
    const float EYE_Y              = 2f;     // 眼珠上下
    const float IDLE_TILT          = 8f;     // 歪头幅度
    const float IDLE_SMILE         = 0.6f;   // 微笑幅度
    const float IDLE_MOUTH         = 0.4f;   // 张嘴幅度
    const float IDLE_BROW          = 6f;     // 眉毛幅度（动作2）
    const float IDLE_BROW_Y        = 6f;     // 眉毛抬起幅度（动作3）

    // -- 丰富静止动作 --

    // 动作1: 星辉环绕（紫环旋转 + 星星闪烁）
    const float SPIN_DURATION      = 6f;
    const float SPIN_RING_OUTER    = 30f;    // 外紫环旋转幅度
    const float SPIN_RING_MID      = 45f;    // 中紫环旋转幅度
    const float SPIN_RING_INNER    = 60f;    // 内紫环旋转幅度

    // 动作2: 伸懒腰（抬右手 + 后仰 + 眯眼张嘴）
    const float STRETCH_DURATION   = 4.5f;
    const float STRETCH_R_ARM      = 25f;    // 右手伸出
    const float STRETCH_BODY_BACK  = -5f;    // 身体后仰
    const float STRETCH_MOUTH_OPEN = 0.6f;   // 张嘴
    const float STRETCH_EYE_CLOSE  = 0.4f;   // 眯眼

    // 动作3: 爱心眨眼（爱心眼 + 歪头微笑）
    const float HEART_DURATION     = 3f;
    const float HEART_EYE          = 1f;     // 爱心眼
    const float HEART_TILT         = 12f;    // 歪头
    const float HEART_SMILE        = 0.8f;   // 微笑

    // 动作7: 数钱钱 💰
    const float MONEY_DURATION     = 3.5f;
    const float MONEY_SMILE        = 0.6f;   // 微笑幅度
    const float MONEY_TILT         = 10f;    // 歪头幅度
    const float MONEY_SWAY         = 4f;     // 身体摇摆幅度

    // 动作8: 委屈 😢
    const float CRY_DURATION       = 3.5f;
    const float CRY_HEAD_DOWN      = 6f;     // 低头幅度
    const float CRY_MOUTH_TREM     = 0.3f;   // 嘴巴微颤
    const float CRY_BROW_UP        = 5f;     // 眉毛抬起（委屈八字眉）

    // 动作10: 害羞黑脸 😊🖤
    const float BLUSH_DURATION     = 3.5f;
    const float BLUSH_DARK         = 1f;     // 黑脸程度
    const float BLUSH_LOOK_AWAY    = -8f;    // 眼神躲闪
    const float BLUSH_SMILE        = 0.5f;   // 害羞微笑

    // 动作11: 困惑 🤔（歪头+皱眉+眯眼，只在 AI 困惑时触发）
    const float CONFUSE_DURATION    = 3f;
    const float CONFUSE_TILT        = 15f;    // 歪头幅度
    const float CONFUSE_BROW        = -3f;    // 皱眉（负=压低）
    const float CONFUSE_EYE_SQUINT  = 0.15f;  // 眯眼幅度
    const float CONFUSE_MOUTH       = 0.2f;   // 微微张嘴
    const float CONFUSE_HEAD_SIDE   = -5f;    // 头侧偏
    const float CONFUSE_BODY_SIDE   = 3f;     // 身体微侧

    // 动作9: 法阵显现 ✨（起势→剑指朝天结印→指尖凝光→扩散至全屏→消散）
    const float CIRCLE_DURATION       = 8.0f;
    // -- 走路（侧面视角）--
    // 模型转体侧面，腿/手臂摆动可见
    // bodyAngleY符号由方向决定（翻转后视觉一致）
    const float WALK_SIDE_ANGLE    = 18f;    // 身体Y轴转体幅度（方向自动匹配）
    const float WALK_SWAY_FREQ     = 5f;     // 步频
    const float WALK_BOUNCE_PX    = 4f;     // 上下颠簸(像素)
    const float WALK_BODY_LEAN    = 5f;     // 身体前倾
    const float WALK_HEAD_TILT    = 8f;     // 头微低看路（ParamAngleY 正数=低头）
    const float WALK_LEG_LIFT     = 4f;     // 抬腿幅度 (Param165)
    const float WALK_LEG_SWING    = 6f;     // 腿前后摆幅 (Param126/129 位移)
    const float WALK_LEG_BEND     = 6f;     // 腿弯曲幅度 (Param127/131 透视)
    const float WALK_ARM_BIG      = 2f;     // 手臂大范围参数 (Param94, 范围[-30,60])
    const float WALK_ARM_SMALL    = 0.4f;   // 手臂小范围参数 (Param31~37, 范围[-1,1])
    const float WALK_BODY_SWING   = 2f;     // 身体Z轴横摆(驱动衣服飘动, ParamBodyAngleZ)
    const float WALK_SHOULDER     = 1.5f;   // 耸肩 (Param153)
    const float WALK_BREATH       = 3f;     // 呼吸恒定加深（给物理持续输入）
    const float IDLE_BLEND_DURATION = 0.4f;  // 走路→空闲混合消退时长
    const float WALK_FADE_IN_DURATION = 0.3f; // 空闲→走路体态淡入时长

    // -- 下落 --
    const float FALL_BODY_ANGLE_X  = -3f;    // 下落身体前倾
    const float FALL_HEAD_ANGLE_X  = -5f;    // 下落头部角度

    // -- 点击 --
    const float CLICK_BODY_ANGLE_X = -5f;    // 点击身体角度
    const float CLICK_HEAD_ANGLE_X = 8f;     // 点击头部角度
    const float CLICK_EYE_OPEN     = 0.3f;   // 点击眯眼
    const float CLICK_LOCK_TIME    = 1.0f;   // 点击姿势锁定秒数

    // -- 点击区域（按 hitNormY 区分：0=头顶，1=脚底）--
    // 头部区 (0~0.35): 摸头 — 眯眼歪头
    // 身体区 (0.35~0.65): 戳身体 — 睁大眼张嘴惊讶
    const float POKE_EYE_OPEN      = 1.3f;   // 戳身体眼睛睁大
    const float POKE_MOUTH_OPEN    = 0.8f;   // 戳身体张嘴惊讶
    const float POKE_MOUTH_FORM    = 0.5f;   // 戳嘴巴型
    const float POKE_BROW_RAISE    = 10f;    // 戳眉毛抬起
    // 腿部区 (0.65~1.0): 碰腿 — 害羞开心（复用 BLUSH 参数）
    const float LEG_HIT_ANGLE_Z    = -6f;    // 碰腿歪头
    const float LEG_HIT_SMILE      = 0.6f;   // 碰腿微笑
    const float LEG_HIT_EYE_CLOSE  = 0.3f;   // 碰腿眯眼

    // -- 屏幕边缘碰撞反弹 --
    const float WALL_HIT_DURATION    = 0.5f;  // 反弹动画持续秒数

    // ===================================================================
    // ⭐4 拖拽挣扎参数 — 改这里调手/脚/头的幅度和频率
    // ===================================================================
    // -- 双臂 --
    const float DRAG_ARM_FREQ         = 4.5f;  // 摆臂频率（越大越急促）
    const float DRAG_RIGHT_AMP        = 3f;   // 右臂摆动幅度 (Param94 主驱动)
    const float DRAG_LEFT_AMP         = 0.1f;   // 左臂摆动幅度
    const float DRAG_JITTER1_FREQ     = 2f;  // 抖动1 频率
    const float DRAG_JITTER1_AMP      = 0.2f; // 抖动1 幅度（占幅度比例）
    const float DRAG_JITTER2_FREQ     = 1f; // 抖动2 频率
    const float DRAG_JITTER2_AMP      = 0.4f; // 抖动2 幅度
    // 右臂关节目录系数（乘以 rightBase，越大该关节动得越明显）
    const float DRAG_RPARAM94         = 1f;  // 右上臂旋转
    const float DRAG_RPARAM97         = 0.2f;  // 基础上臂旋转
    const float DRAG_RPARAM31         = 0.25f;  // 前臂
    const float DRAG_RPARAM32         = 0.1f;  // R2
    const float DRAG_RPARAM33         = 0.2f;  // 上臂
    const float DRAG_RPARAM93         = 0f;  // 手形切换（设为 0=常开，1=随幅度变化）
    const float DRAG_RPARAM118        = 0.6f;  // 右手伸出
    // 右臂透视图层系数（0=隐藏，1=全浮出）
    const float DRAG_LAYER95          = 0.8f;
    const float DRAG_LAYER117         = 0.5f;
    const float DRAG_LAYER98          = 0.6f;
    const float DRAG_LAYER100         = 0.6f;
    const float DRAG_LAYER116         = 0.4f;
    const float DRAG_LAYER120         = 0.8f;
    const float DRAG_LAYER108         = 0.8f;
    const float DRAG_LAYER119         = 0.8f;
    // 左臂关节目录系数（乘以 leftBase，负号自行在方法中用）
    const float DRAG_LPARAM34         = 0.1f;  // 左臂L1
    const float DRAG_LPARAM36         = 0.1f;  // 左臂L2
    const float DRAG_LPARAM37         = 0.1f;  // 左臂L3

    // -- 双腿 --
    const float DRAG_LEG_FREQ         = 5.0f;  // 踏步频率
    const float DRAG_LEG_SWING        = 12f;   // 腿前后摆幅 (Param126/129)
    const float DRAG_LEG_BEND         = 6f;    // 腿弯曲幅度 (Param127/131)
    const float DRAG_LEG_LIFT         = 8f;    // 抬腿幅度 (Param165/164)

    // -- 身体/头部（鼠标速度驱动 → 物理自然推导头发/法盘/裙子）--
    const float DRAG_TURN_ANGLE       = 10f;   // 拖拽转身角度 (ParamBodyAngleY, +朝右转)
    const float DRAG_TURN_SMOOTH      = 0.1f;  // 转身平滑速度（越大反应越快）
    const float DRAG_BODY_SWAY        = 5f;    // 身体左右扭动幅度 (ParamBodyAngleX)
    const float DRAG_BODY_FREQ        = 2.0f;  // 身体扭动频率
    // -- 速度→输入参数 + 直接驱动裙子/法盘（全部同方向，参考走路物理方向）--
    const float DRAG_VEL_LERP       = 0.01f;  // 速度平滑（越小越滑）
    const float DRAG_VEL_MAX        = 3f;      // 原始速度上限（防瞬冲，越大响应越快）
    const float DRAG_BODY_Z_SCALE   = 3f;     // 速度→ParamBodyAngleZ（给物理）
    const float DRAG_BODY_Z_MAX     = 12f;
    const float DRAG_DIRECT_SCALE   = 4f;     // 速度→直接驱动 Param82/87/84/49/51/57/60
    const float DRAG_DIRECT_MAX     = 35f;    // 直接驱动最大值
    const float DRAG_HEAD_X_SCALE   = 1.8f;   // 速度→ParamAngleX
    const float DRAG_HEAD_X_MAX     = 22f;
    const float DRAG_HEAD_Z_SCALE   = 1.2f;   // 速度→ParamAngleZ
    const float DRAG_HEAD_Z_MAX     = 16f;
    // -- 头发直接驱动参数（物理 Delay 太高，直接接管）--
    const float DRAG_HAIR_SCALE     = 0.8f;   // 速度→头发（相对于 d 的比例）
    const float DRAG_HAIR_MAX       = 20f;    // 头发驱动最大值
    const float DRAG_HAIR169_SCALE  = 0.6f;   // Param169 饰品头饰驱动
    const float DRAG_HAIR169_MAX    = 15f;
    const float DRAG_HEAD_SHAKE       = 5f;    // 头部左右摆动幅度 (ParamAngleX)
    const float DRAG_HEAD_SHAKE_FREQ  = 3.5f;  // 头部摆动频率
    const float DRAG_HEAD_TILT        = -2f;   // 头部后仰基准 (ParamAngleY，正=抬头)
    const float DRAG_HEAD_BOB         = 1f;    // 头部上下抖动幅度
    const float DRAG_HEAD_BOB_FREQ    = 2.0f;  // 头部上下抖动频率

    // -- 表情 --
    const float DRAG_EYE_OPEN         = 1.1f;  // 眼睛睁开幅度（1=正常，>1=睁大）
    const float DRAG_MOUTH_AMP        = 0.5f;  // 嘴巴张开基值
    const float DRAG_MOUTH_PULSE      = 0.3f;  // 嘴巴随呼吸波动幅度
    const float DRAG_MOUTH_FREQ       = 5.0f;  // 嘴巴波动频率
    const float DRAG_MOUTH_PHASE      = 1.0f;  // 嘴波动相位偏移
    const float DRAG_BROW             = 1.2f;  // 眉毛抬起幅度（>1=高抬）
    // ===================================================================
    const float WALL_HIT_EYE_OPEN    = 1.3f;  // 瞪眼幅度
    const float WALL_HIT_MOUTH_OPEN  = 0.5f;  // 张嘴幅度
    const float WALL_HIT_BODY_LEAN   = 8f;    // 身体后仰幅度

    // -- 鼠标跟随眼睛 --
    const float EYE_FOLLOW_DISTANCE  = 150f;  // 鼠标在此距离内触发眼睛跟随（像素）
    const float EYE_FOLLOW_MAX_X     = 10f;   // 眼珠最大水平偏移
    const float EYE_FOLLOW_MAX_Y     = 8f;    // 眼珠最大垂直偏移
    // ==================================================
    [Header("模型 Prefab")]
    [Tooltip("Cubism SDK 导入后生成的模型 Prefab（不拖也行，代码自动按路径加载）")]
    public GameObject modelPrefab;

    [Header("显示设置（改顶部 LIVE2D_SCALE / LIVE2D_OFFSET_Y 宏）")]
    [Tooltip("模型缩放")]
    public float modelScale = LIVE2D_SCALE;

    [Tooltip("模型垂直偏移（像素）")]
    public float verticalOffset = LIVE2D_OFFSET_Y;

    // Cubism 组件
    private GameObject _modelRoot;
    private CubismModel _cubismModel;
    private CubismPhysicsController _physicsController;

    // DesktopPet 引用
    private DesktopPet _pet;
    private DragHandler _dragHandler;
    private ChatBubble _chatBubble;
    private TimeWeatherController _timeController;

    // 姿势锁定
    private bool _poseLocked = false;
    private float _poseLockUntil = 0f;
    // 点击姿势保存的参数（物理 order 800 会覆盖，LateUpdate 重新设）
    private readonly Dictionary<string, float> _clickSavedParams = new Dictionary<string, float>();

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
    private int _currentIdleAction = 0; // 0=无, 1=歪头, 2=微笑, 3=挑眉, 4=星辉, 5=伸懒腰, 6=爱心眼, 7=数钱, 8=委屈, 9=法阵, 10=害羞, 11=困惑
    // 各动作权重（对应动作 1-11），值越大出现概率越高（11号权重0=不自发触发，仅外部强制调用）
    private readonly int[] _idleActionWeights = new int[] { 5, 5, 3, 2, 2, 3, 2, 3, 1, 3, 0 };
    // 复合动作相位（用于多参数协同插值）
    private float _complexActionPhase = 0f;
    // 动作结束后的冷却时间（防动作无限重播）
    private float _idleActionCooldown = 0f;

    // 随机微动用噪声偏移
    private float _noiseTimeX = 0f;
    private float _noiseTimeY = 0f;

    // 强制动作锁定（右键菜单触发，播放期间不被走路覆盖）
    private bool _actionLocked = false;
    public event System.Action OnForcedActionFinished;

    // ===== 调试偏移系统（DebugWindow 实时调参） =====
    /// <summary>是否启用调试偏移</summary>
    public bool debugOffsetEnabled = false;
    /// <summary>调试偏移表：参数名 → 偏移量（在动画值上叠加，每帧重新应用不累积）</summary>
    public Dictionary<string, float> debugOffsets = new Dictionary<string, float>();

    // 是否已加载
    private bool _loaded = false;

    // 走路颠簸当前偏移量（像素）
    private float _walkBounceOffset = 0f;

    // 走路相位
    private float _walkPhase = 0f;

    // 上一帧是否在走路（用于检测走路↔空闲切换）
    private bool _wasWalkingLastFrame = false;

    // 走路→空闲混合消退计时
    private float _walkBlendRemaining = 0f;

    // 空闲→走路体态淡入计时（动作结束后切走路不生硬）
    private float _walkFadeInRemaining = 0f;

    // 屏幕边缘碰撞反弹
    private float _wallHitTime = 0f;

    // 鼠标眼睛跟随目标（null=使用默认 Perlin 噪声）
    private float? _eyeTargetX = null;
    private float? _eyeTargetY = null;

    // 拖拽平滑转身
    private float _dragSmoothBodyY = 0f;
    // 拖拽速度追踪（帧间 petX 增量，平滑后驱动物理和输出）
    private float _dragSmoothBodyZ = 0f;   // 身体/裙子/法盘输入
    private float _dragSmoothHeadX = 0f;   // 头左右输入
    private float _dragSmoothHeadZ = 0f;   // 头旋转输入
    private int _lastDragPetX = 0;
    private bool _dragInited = false;
    // 平滑眼睛跟随（防突变）
    private float _eyeSmoothX = 0f;
    private float _eyeSmoothY = 0f;
    private bool _eyeSmoothActive = false;

    private void Start()
    {
        Debug.Log("[Live2DRenderer] Start() 被调用了");
        _pet = GetComponent<DesktopPet>();
        _dragHandler = GetComponent<DragHandler>();
        _chatBubble = GetComponent<ChatBubble>();
        if (_chatBubble == null) _chatBubble = FindObjectOfType<ChatBubble>();
        _timeController = GetComponent<TimeWeatherController>();
        if (_timeController == null) _timeController = FindObjectOfType<TimeWeatherController>();
        Debug.Log($"[Live2DRenderer] DesktopPet={(_pet != null)}, DragHandler={(_dragHandler != null)}, ChatBubble={(_chatBubble != null)}, TimeWeatherController={(_timeController != null)}");

        // ★ 强制从宏读取（忽略场景中序列化的旧值，改宏立即生效）
        modelScale = LIVE2D_SCALE;
        verticalOffset = LIVE2D_OFFSET_Y;

        TryLoadModel();
    }

    private void TryLoadModel()
    {
        Debug.Log($"[Live2DRenderer] TryLoadModel() modelPrefab当前值={modelPrefab}");
        // ★ 无条件优先用 AssetDatabase 按路径加载（场景序列化引用可能损毁）
        #if UNITY_EDITOR
        string prefabPath = "Assets/Live2D/Models/Fuxuan/符玄.prefab";
        Debug.Log($"[Live2DRenderer] 尝试 AssetDatabase 加载: {prefabPath}");
        GameObject resolvedPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
        Debug.Log($"[Live2DRenderer] AssetDatabase 结果={(resolvedPrefab != null ? resolvedPrefab.name : "null")}");
        if (resolvedPrefab != null)
        {
            modelPrefab = resolvedPrefab;
        }
        #endif
        // 降级：Resources.Load（需要把 prefab 放 Resources 文件夹下）
        if (modelPrefab == null)
        {
            modelPrefab = Resources.Load<GameObject>("Live2D/Models/Fuxuan/符玄");
        }

        if (modelPrefab != null)
        {
            _modelRoot = Instantiate(modelPrefab, transform);
            
            // 设置 Layer（确保 Camera 能看到）
            SetLayerRecursively(_modelRoot, 0); // 0 = Default
            
            Debug.Log($"[Live2DRenderer] _modelRoot={_modelRoot.name}, activeSelf={_modelRoot.activeSelf}, activeInHierarchy={_modelRoot.activeInHierarchy}");
            Debug.Log($"[Live2DRenderer] _modelRoot.transform.childCount={_modelRoot.transform.childCount}");

            _cubismModel = _modelRoot.GetComponentInChildren<CubismModel>();
            _physicsController = _modelRoot.GetComponentInChildren<CubismPhysicsController>();

            // 列出所有子物体和组件
            Debug.Log($"[Live2DRenderer] 模型实例化后结构:");
            ListAllChildren(_modelRoot, 0);

            // 检查 CubismRenderController
            var renderController = _modelRoot.GetComponentInChildren<Live2D.Cubism.Rendering.CubismRenderController>();
            Debug.Log($"[Live2DRenderer] CubismRenderController={renderController != null}");
            if (renderController != null)
            {
                Debug.Log($"[Live2DRenderer] CubismRenderController.enabled={renderController.enabled}");
            }

            _loaded = true;
            Debug.Log($"[Live2DRenderer] 模型 Prefab 实例化完成, CubismModel={_cubismModel != null}, Physics={_physicsController != null}");
            if (_cubismModel != null)
            {
                Debug.Log($"[Live2DRenderer] 参数数量: {_cubismModel.Parameters.Length}");
                // ★ 打印所有参数名 + 范围，找真实的手部参数
                string allParams = "";
                foreach (var p in _cubismModel.Parameters)
                    allParams += p.Id + ", ";
                Debug.Log("[Live2DRenderer] 所有参数: " + allParams);

                // ★ 打印重点参数的范围（单位类型 + 最小/最大/默认值）
                string[] keyParams = new string[] {
                    "Param93", "Param94", "Param92", "Param118", 
                    "Param33", "Param31", "Param32", "Param97",
                    "Param95", "Param117", "Param98", "Param100", 
                    "Param116", "Param120", "Param108", "Param119",
                    "Param34", "Param36", "Param37",
                    "Param110", "Param111", "Param99",
                    "Param112", "Param113", "Param114", "Param115"
                };
                string rangeInfo = "[Live2DRenderer] 关键参数范围: ";
                foreach (var pid in keyParams)
                {
                    var p = _cubismModel.Parameters.FindById(pid);
                    if (p != null)
                        rangeInfo += $"{pid}(min={p.MinimumValue:F1},max={p.MaximumValue:F1},def={p.DefaultValue:F1}) ";
                }
                Debug.Log(rangeInfo);
            }
        }

        if (!_loaded)
        {
            Debug.LogError("[Live2DRenderer] 没有设置 modelPrefab，请在 Inspector 中拖拽 Cubism 导入的模型 Prefab");
            enabled = false;
        }
    }
    
    private System.Collections.IEnumerator CheckRendererStatus()
    {
        yield return new WaitForSeconds(0.1f); // 延迟检查
        if (_modelRoot != null)
        {
            var renderers = _modelRoot.GetComponentsInChildren<Renderer>();
            int enabledCount = 0;
            int disabledCount = 0;
            foreach (var renderer in renderers)
            {
                if (renderer.enabled)
                    enabledCount++;
                else
                    disabledCount++;
            }
            Debug.Log($"[Live2DRenderer] 延迟检查 - 总共: {renderers.Length}, 启用: {enabledCount}, 禁用: {disabledCount}");
            
            // 强制启用所有 Renderer
            if (disabledCount > 0)
            {
                int forceEnabled = 0;
                foreach (var renderer in renderers)
                {
                    if (!renderer.enabled)
                    {
                        renderer.enabled = true;
                        forceEnabled++;
                    }
                }
                Debug.Log($"[Live2DRenderer] 强制启用了 {forceEnabled} 个 Renderer");
            }
        }
    }

    private void Update()
    {
        if (!_loaded || _cubismModel == null) return;

        // ★ 拖拽中不累积走路相位、不走体态逻辑（由 UpdateDragStruggle 接管身体参数给物理）
        if (_pet != null && _pet.isDragging)
        {
            _walkPhase = 0f;
            _walkBounceOffset = 0f;
            UpdateModelPosition();
            return;
        }

        // 累积走路相位并计算垂直颠簸偏移
        if (_pet != null && _pet.onGround && _pet.petVx != 0)
        {
            _walkPhase += Time.deltaTime * WALK_SWAY_FREQ;
            // 限制范围防精度损失（≈1个完整周期）
            if (_walkPhase > Mathf.PI * 2f) _walkPhase -= Mathf.PI * 2f;
            // ★ 颠簸（1 - abs(sin) = 腿并拢时最高，迈步时最低）
            _walkBounceOffset = (1f - Mathf.Abs(Mathf.Sin(_walkPhase))) * WALK_BOUNCE_PX;
        }
        else
        {
            _walkBounceOffset = 0f;
            _walkPhase = 0f;
        }

        UpdateModelPosition();

        // ★ 体态提前给物理用：Physics 在 CubismUpdateController.LateUpdate(0)
        //   中读取 ParamBodyAngleX/Y/Z 来驱动衣服。我们在 Update() 中先设好
        //   走路的转体/前倾/低头，确保物理拿到正确的体态输入。
        bool isWalking = (_pet != null && _pet.onGround && _pet.petVx != 0 && !_pet.isPaused && !_actionLocked);
        if (isWalking)
        {
            float bodyWeight = 1f;
            if (_walkFadeInRemaining > 0f)
            {
                float raw = 1f - Mathf.Clamp01(_walkFadeInRemaining / WALK_FADE_IN_DURATION);
                bodyWeight = raw * raw;
                _walkFadeInRemaining -= Time.deltaTime;
            }
            ApplyWalkBodyPose(bodyWeight);
        }
        else if (_wasWalkingLastFrame || _walkBlendRemaining > 0f)
        {
            // 过渡帧：_walkBlendRemaining 要到 LateUpdate 才设，但物理在 LateUpdate(0) 就要读体态了
            // 用 _wasWalkingLastFrame 兜住「刚停的第一帧」不设体态的空窗期
            float blendWeight = Mathf.Clamp01(_walkBlendRemaining / IDLE_BLEND_DURATION);
            float eased = blendWeight * blendWeight;
            ApplyWalkBodyPose(eased);
        }
    }

    private void LateUpdate()
    {
        if (!_loaded || _cubismModel == null) return;

        // ★ 拖拽中 → 物理(order 800)已跑完，挣扎参数被物理覆盖了，在这里重新设一遍
        if (_pet != null && _pet.isDragging)
        {
            // 平滑转身（_dragSmoothBodyY 在 UpdateDragStruggle 中更新）
            UpdateDragStruggle();
            if (_cubismModel != null) _cubismModel.ForceUpdateNow();
            return;
        }

        // ★ 点击/摸头锁定中 → 重新设置被物理覆盖的参数
        if (_poseLocked && Time.time < _poseLockUntil)
        {
            foreach (var kv in _clickSavedParams)
                SetParameter(kv.Key, kv.Value);
            if (_cubismModel != null) _cubismModel.ForceUpdateNow();
            return;
        }

        // 累积噪声时间
        _noiseTimeX += Time.deltaTime * 0.6f;
        _noiseTimeY += Time.deltaTime * 0.4f;
        _breathPhase += Time.deltaTime * 2.0f;

        UpdateBlink();

        // ★ 走路/空闲统一在 LateUpdate 中设置参数
        // 此时 _walkPhase 已在 Update() 中更新完毕，相位准确
        bool isWalking = (_pet != null && _pet.onGround && _pet.petVx != 0 && !_pet.isPaused && !_actionLocked);

        if (isWalking)
        {
            if (!_wasWalkingLastFrame)
            {
                // 空闲→走路：清表情残留 + 开始体态淡入
                ResetIdleAction();
                _walkBlendRemaining = 0f;
                _walkFadeInRemaining = WALK_FADE_IN_DURATION;
            }
            // 走路淡入权重（手脚幅度从0渐增至1）
            float walkAnimWeight = 1f;
            if (_walkFadeInRemaining > 0f)
            {
                float raw = 1f - Mathf.Clamp01(_walkFadeInRemaining / WALK_FADE_IN_DURATION);
                walkAnimWeight = raw * raw; // ease-in
            }
            UpdateWalkAnimation(walkAnimWeight);
        }
        else
        {
            if (_wasWalkingLastFrame)
            {
                // 走路→空闲：开始混合消退
                _walkBlendRemaining = IDLE_BLEND_DURATION;
                _walkFadeInRemaining = 0f; // 重置淡入（下次空闲→走路重新开始）
            }

            if (_walkBlendRemaining > 0f)
            {
                // ⭐3 无缝过渡：混合期同时播走路（渐消）和空闲动画
                float blendWeight = Mathf.Clamp01(_walkBlendRemaining / IDLE_BLEND_DURATION);
                // 先播空闲动画（覆盖基础呼吸/Perlin/眼睛）
                UpdateIdleAnimation();
                // 再叠加消退中的走路动画参数（手臂/腿逐渐停止）
                UpdateWalkAnimation(blendWeight);
                _walkBlendRemaining -= Time.deltaTime;
            }
            else
            {
                UpdateIdleAnimation();
            }
        }

        _wasWalkingLastFrame = isWalking;

        // ★ 屏幕边缘碰撞反弹动画：覆盖在现有参数之上
        if (_wallHitTime > 0f)
        {
            float t = _wallHitTime / WALL_HIT_DURATION; // 1→0
            float progress = Mathf.Clamp01(t);

            // 瞪眼（出场的头部+身体角度混合）
            float eyeOpen = Mathf.Lerp(WALL_HIT_EYE_OPEN, 1f, progress);
            SetParameter("ParamEyeLOpen", eyeOpen);
            SetParameter("ParamEyeROpen", eyeOpen);

            // 张嘴（渐消）
            float mouthOpen = Mathf.Lerp(WALL_HIT_MOUTH_OPEN, 0f, progress * progress);
            SetParameter("ParamMouthOpenY", mouthOpen);

            // 身体后仰（迅速弹回）
            float bodyLean = Mathf.Lerp(WALL_HIT_BODY_LEAN, 0f, progress * 2f);
            SetParameter("ParamBodyAngleX", bodyLean);

            // 头部微缩（受惊）
            float headBack = Mathf.Lerp(3f, 0f, progress);
            SetParameter("ParamAngleY", headBack);

            _wallHitTime -= Time.deltaTime;

            // 强制刷新让网格同步
            _cubismModel.ForceUpdateNow();
        }

        // ★ 鼠标眼睛跟随覆盖：在所有动画参数之后、ForceUpdateNow 之前设置
        bool eyeOverridden = (_eyeTargetX.HasValue || _eyeTargetY.HasValue);
        if (eyeOverridden)
        {
            // 平滑追踪目标值（用 lerp 防止眼球突变）
            float rawTargetX = (_eyeTargetX ?? 0f) * EYE_FOLLOW_MAX_X;
            float rawTargetY = (_eyeTargetY ?? 0f) * EYE_FOLLOW_MAX_Y;

            _eyeSmoothX = Mathf.Lerp(_eyeSmoothX, rawTargetX, 0.08f);
            _eyeSmoothY = Mathf.Lerp(_eyeSmoothY, rawTargetY, 0.08f);
            _eyeSmoothActive = true;

            SetParameter("ParamEyeBallX", _eyeSmoothX);
            SetParameter("ParamEyeBallY", _eyeSmoothY);
        }
        else if (_eyeSmoothActive)
        {
            // 缓慢退回中心（让眼球自然回归，不跳）
            _eyeSmoothX = Mathf.Lerp(_eyeSmoothX, 0f, 0.04f);
            _eyeSmoothY = Mathf.Lerp(_eyeSmoothY, 0f, 0.04f);
            SetParameter("ParamEyeBallX", _eyeSmoothX);
            SetParameter("ParamEyeBallY", _eyeSmoothY);
            if (Mathf.Abs(_eyeSmoothX) < 0.1f && Mathf.Abs(_eyeSmoothY) < 0.1f)
                _eyeSmoothActive = false;
        }

        // ★ 强制网格更新：Cubism 的网格在 Update() 阶段已用 C++ 核心算完，
        //    Physics(800) 覆盖了衣服参数，我们(801)覆盖了手臂参数，
        //    但网格仍是旧参数结果，需强制刷新用最新参数重新算一遍。
        if (isWalking || _walkBlendRemaining > 0f || eyeOverridden)
        {
            _cubismModel.ForceUpdateNow();
        }

        // ★ 调试偏移通道：动画完成后，在动画值上叠加偏移量
        // 因动画每帧重新设值，偏移不会累积（动画值 + 偏移 = 最终值）
        // 任一空闲动作运行时暂停偏移，避免破坏动画手部/手指姿态
        bool hasActiveAction = (_currentIdleAction > 0 && _idleActionTime > 0f);
        if (debugOffsetEnabled && !_actionLocked && !hasActiveAction && debugOffsets != null && debugOffsets.Count > 0)
        {
            foreach (var kv in debugOffsets)
            {
                var p = _cubismModel.Parameters.FindById(kv.Key);
                if (p != null)
                {
                    float animVal = p.Value;                      // 动画已设置的值
                    p.Value = Mathf.Clamp(animVal + kv.Value,     // 叠加偏移
                        p.MinimumValue, p.MaximumValue);
                }
            }

            // ★ 自动设置手部图层/透视参数，让手浮到衣服前面
            SetParameter("Param95", 1f);
            SetParameter("Param117", 0.8f);
            SetParameter("Param98", 0.8f);
            SetParameter("Param100", 0.8f);
            SetParameter("Param116", 0.6f);
            SetParameter("Param120", 1f);
            SetParameter("Param108", 1f);
            SetParameter("Param119", 1f);

            // ★ 强制 Cubism 重新计算网格变形
            _cubismModel.ForceUpdateNow();
        }

        // ============================================================
        // ★ 待机气泡：>30秒无交互时头顶冒泡
        // ============================================================
        float idleDuration = (_dragHandler != null)
            ? Time.time - _dragHandler.lastInteractionTime : 0f;
        if (_dragHandler != null && _chatBubble != null)
        {
            UpdateIdleBubble(idleDuration);
        }
    }

    /// <summary>
    /// 待机气泡逻辑：长时间无交互时偶尔冒泡
    /// </summary>
    private float _idleBubbleTimer = 0f;
    private bool _idleBubbleShown = false;
    private static readonly string[] IDLE_BUBBLES = new string[]
    {
        "嗯…好无聊~",
        "好安静呀…",
        "有人吗~~",
        "想去散步…",
        "想喝奶茶…",
        "ZZZ…",
        "你在干嘛呢？",
        "好闲哦…",
        "今天天气怎么样~",
        "要不要一起玩？"
    };

    private void UpdateIdleBubble(float idleDuration)
    {
        // 无交互超过 30 秒，进入待机气泡状态
        if (idleDuration >= 30f && !_pet.isPaused && !_pet.isDragging)
        {
            if (!_idleBubbleShown)
            {
                _idleBubbleTimer = 0f;
                _idleBubbleShown = true;
            }
            else
            {
                _idleBubbleTimer += Time.deltaTime;
                // 每 15~30 秒随机冒泡一次
                if (_idleBubbleTimer >= Random.Range(15f, 30f))
                {
                    if (!_chatBubble.IsShowing)
                    {
                        string msg = PickIdleBubbleMessage();
                        _chatBubble.ShowMessage(msg, 4f);
                    }
                    _idleBubbleTimer = 0f;
                }
            }
        }
        else
        {
            _idleBubbleShown = false;
            _idleBubbleTimer = 0f;
        }
    }

    /// <summary>
    /// 选取待机气泡消息（50% 概率随机池，50% 概率天气/时间特化）
    /// </summary>
    private string PickIdleBubbleMessage()
    {
        bool useTimeWeather = (_timeController != null) && (Random.value < 0.5f);
        if (useTimeWeather)
        {
            // 时间特化
            if (_timeController.isSleepyTime)
                return SLEEPY_BUBBLES[Random.Range(0, SLEEPY_BUBBLES.Length)];
            else if (_timeController.isNight)
                return NIGHT_BUBBLES[Random.Range(0, NIGHT_BUBBLES.Length)];
            else if (_timeController.hour >= 5 && _timeController.hour < 8)
                return MORNING_BUBBLES[Random.Range(0, MORNING_BUBBLES.Length)];

            // 天气特化
            if (_timeController.weatherFetched)
            {
                var wt = _timeController.weather;
                string tempLabel = _timeController.temperatureC < 5f ? "好冷" :
                                   _timeController.temperatureC > 30f ? "好热" : null;
                if (wt == TimeWeatherController.WeatherType.Rain ||
                    wt == TimeWeatherController.WeatherType.Drizzle)
                {
                    string[] rainMsgs = tempLabel != null
                        ? new string[] { $"下雨了{tempLabel}…", "听雨声发呆~", "淅淅沥沥…" }
                        : new string[] { "下雨了呢~", "听雨声发呆~", "淅淅沥沥…" };
                    return rainMsgs[Random.Range(0, rainMsgs.Length)];
                }
                if (wt == TimeWeatherController.WeatherType.Thunder)
                    return THUNDER_BUBBLES[Random.Range(0, THUNDER_BUBBLES.Length)];
                if (wt == TimeWeatherController.WeatherType.Snow)
                    return SNOW_BUBBLES[Random.Range(0, SNOW_BUBBLES.Length)];
                if (wt == TimeWeatherController.WeatherType.Clear)
                    return SUNNY_BUBBLES[Random.Range(0, SUNNY_BUBBLES.Length)];
                if (tempLabel != null)
                    return $"今天{tempLabel}呀…";
            }
        }
        return IDLE_BUBBLES[Random.Range(0, IDLE_BUBBLES.Length)];
    }

    private static readonly string[] SLEEPY_BUBBLES = new string[]
    {
        "好晚了…该睡了~",
        "哈欠~~困了…",
        "眼睛快睁不开了…",
        "想躺床上…",
        "再玩一会就睡…Zzz",
    };

    private static readonly string[] NIGHT_BUBBLES = new string[]
    {
        "晚上好呀~",
        "月色真美…",
        "一个人在晚上有点寂寞呢~",
        "还在熬夜吗？",
    };

    private static readonly string[] MORNING_BUBBLES = new string[]
    {
        "早安~",
        "早呀！又是新的一天~",
        "哈欠…早上好…",
        "今天也要加油哦！",
    };

    private static readonly string[] THUNDER_BUBBLES = new string[]
    {
        "打雷了好可怕！",
        "轰隆隆…好吓人…",
        "雷雨天气要注意安全~",
    };

    private static readonly string[] SNOW_BUBBLES = new string[]
    {
        "下雪了！好漂亮~",
        "雪白白的好美~",
        "想堆雪人…",
    };

    private static readonly string[] SUNNY_BUBBLES = new string[]
    {
        "今天天气真好~",
        "太阳暖洋洋的~",
        "好天气让人心情好~",
        "想出去晒太阳~",
    };

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

        // === 眼球 — 鼠标跟随覆盖由 LateUpdate 末尾统一处理 ===
        // 默认 Perlin 噪声自然微动（如无鼠标目标）
        float eyeX = (Mathf.PerlinNoise(_noiseTimeY, 6f) - 0.5f) * EYE_X;
        float eyeY = (Mathf.PerlinNoise(_noiseTimeY, 7f) - 0.5f) * EYE_Y;
        SetParameter("ParamEyeBallX", eyeX);
        SetParameter("ParamEyeBallY", eyeY);

        // === 昼夜/天气基调表情 ===
        if (_timeController != null)
        {
            float nightDroop = 0f;
            if (_timeController.isSleepyTime) nightDroop = 0.15f;       // 22~5点：眼皮微垂
            else if (_timeController.isNight) nightDroop = 0.07f;       // 18~22点：轻微
            if (nightDroop > 0f && !_isBlinking)
            {
                SetParameter("ParamEyeLOpen", Mathf.Lerp(1f, 0.7f, nightDroop));
                SetParameter("ParamEyeROpen", Mathf.Lerp(1f, 0.7f, nightDroop));
            }

            // 天气基调
            if (_timeController.weatherFetched)
            {
                var wt = _timeController.weather;
                if (wt == TimeWeatherController.WeatherType.Rain ||
                    wt == TimeWeatherController.WeatherType.Drizzle ||
                    wt == TimeWeatherController.WeatherType.Thunder ||
                    wt == TimeWeatherController.WeatherType.Overcast)
                {
                    // 阴雨 → 轻度委屈：眉毛微抬 + 嘴巴微嘟
                    SetParameter("ParamBrowRY", Mathf.Lerp(0f, 4f, 0.3f));
                    SetParameter("ParamBrowLY", Mathf.Lerp(0f, 4f, 0.3f));
                    SetParameter("ParamMouthForm", Mathf.Lerp(0f, 0.2f, 0.3f));
                }
                else if (wt == TimeWeatherController.WeatherType.Clear ||
                         wt == TimeWeatherController.WeatherType.Cloudy)
                {
                    // 晴/多云 → 自然微笑
                    SetParameter("ParamMouthForm", Mathf.Lerp(0f, 0.2f, 0.2f));
                }
                else if (wt == TimeWeatherController.WeatherType.Snow)
                {
                    // 下雪 → 微微张嘴（好奇）
                    SetParameter("ParamMouthOpenY", Mathf.Lerp(0f, 0.4f, 0.2f));
                    SetParameter("ParamEyeLOpen", Mathf.Lerp(1f, 1.2f, 0.15f));
                    SetParameter("ParamEyeROpen", Mathf.Lerp(1f, 1.2f, 0.15f));
                }
            }
        }

        // === 空闲动作：加权随机选取（权重越高越容易出现）===
        // 动作: 1=歪头, 2=微笑, 3=挑眉, 4=星辉, 5=伸懒腰, 6=爱心眼, 7=数钱, 8=委屈, 9=法阵, 10=害羞

        // ★ 暂停时（菜单打开），不播新动作
        bool isPaused = (_pet != null && _pet.isPaused);

        // 动作冷却衰减
        if (_idleActionCooldown > 0f) _idleActionCooldown -= Time.deltaTime;

        // 当前动作结束后，加权随机选取下一个（走路时不触发，需冷却）
        if (_currentIdleAction == 0 && !isPaused && _idleActionCooldown <= 0f)
        {
            _currentIdleAction = PickWeightedIdleAction();
            _idleActionTime = 0f;
            _complexActionPhase = 0f;
            Debug.Log($"[Live2DRenderer] ▶ 动作 #{_currentIdleAction}");
        }

        if (_currentIdleAction > 0)
        {
            _idleActionTime += Time.deltaTime;
            _complexActionPhase += Time.deltaTime;

            switch (_currentIdleAction)
            {
                case 1: UpdateIdleTilt(); break;
                case 2: UpdateIdleSmile(); break;
                case 3: UpdateIdleBrow(); break;
                case 4: UpdateStarSpin(); break;
                case 5: UpdateStretch(); break;
                case 6: UpdateHeartEyes(); break;
                case 7: UpdateMoney(); break;
                case 8: UpdateCry(); break;
                case 9: UpdateMagicCircle(); break;
                case 10: UpdateBlush(); break;
                case 11: UpdateConfuse(); break;
            }
        }
    }

    #region 空闲随机动作

    /// <summary>动作1: 歪头 — 往一侧歪头卖萌</summary>
    private void UpdateIdleTilt()
    {
        float duration = 2f;
        float t = Mathf.Sin(_idleActionTime / duration * Mathf.PI);
        SetParameter("ParamAngleZ", t * IDLE_TILT);
        if (_idleActionTime >= duration) ResetIdleAction();
    }

    /// <summary>动作2: 微笑 — 眯眼微笑</summary>
    private void UpdateIdleSmile()
    {
        float duration = 2f;
        float t = Mathf.Sin(_idleActionTime / duration * Mathf.PI);
        SetParameter("ParamEyeLSmile", t * IDLE_SMILE);
        SetParameter("ParamEyeRSmile", t * IDLE_SMILE);
        SetParameter("ParamMouthForm", t * IDLE_MOUTH);
        if (_idleActionTime >= duration) ResetIdleAction();
    }

    /// <summary>动作3: 挑眉 — 眉毛微动</summary>
    private void UpdateIdleBrow()
    {
        float duration = 2f;
        float t = Mathf.Sin(_idleActionTime / duration * Mathf.PI);
        SetParameter("ParamBrowRY", t * IDLE_BROW_Y);
        SetParameter("ParamBrowLY", t * IDLE_BROW_Y);
        if (_idleActionTime >= duration) ResetIdleAction();
    }

    /// <summary>
    /// 动作4: 星辉环绕 ✨（已移除视觉特效，仅保留动作时长）
    /// </summary>
    private void UpdateStarSpin()
    {
        float duration = SPIN_DURATION;
        float t = Mathf.Clamp01(_complexActionPhase / duration);

        if (t >= 1f) ResetIdleAction();
    }

    /// <summary>
    /// 动作5: 伸懒腰 🥱
    /// 右手举高 + 身体后仰 + 眯眼张嘴
    /// </summary>
    private void UpdateStretch()
    {
        float p = _complexActionPhase;
        float duration = STRETCH_DURATION;

        float t = Mathf.Clamp01(p / duration);

        // 0→1 (起) 然后保持到快结束
        float rise = Mathf.Clamp01(t * 3f);
        float hold = Mathf.Clamp01((1f - t) * 3f);
        float phase = Mathf.Min(rise, hold); // 梯形：快速起→保持→快速落

        // ★ 右臂全套抬升参数
        SetParameter("Param31", phase * 4f);   // 右臂R1（前臂）
        SetParameter("Param32", phase * 3f);   // 右臂R2
        SetParameter("Param33", phase * 5f);   // 右臂R1（上臂）
        SetParameter("Param94", phase * 10f);  // 右手上臂旋转
        SetParameter("Param97", phase * 3f);   // 右手 基础上臂旋转
        SetParameter("Param95", phase * 0.8f); // 右手 基础 上壁透视
        SetParameter("Param117", phase * 0.5f);// 右手 基础 上壁透视2
        SetParameter("Param98", phase * 0.6f); // 右手 基础下壁透视
        SetParameter("Param100", phase * 0.6f);// 右手 基础手 透视
        SetParameter("Param116", phase * 0.4f);// 透视2
        SetParameter("Param120", phase * 0.8f);// 手 向前透视效果
        SetParameter("Param108", phase * 0.8f);// 右手 基础 图层顺序
        SetParameter("Param119", phase * 0.8f);// 伸手 图层调整
        SetParameter("Param93", phase);         // 右手 基础 切换
        SetParameter("Param118", phase * 0.6f); // 右手伸出参数
        // 左臂配合
        SetParameter("Param34", -phase * 3f);   // 左臂L1
        SetParameter("Param36", -phase * 2f);   // 左臂L2
        SetParameter("Param37", -phase * 1.5f); // 左臂L3

        // 身体后仰
        SetParameter("ParamBodyAngleX", phase * STRETCH_BODY_BACK);
        SetParameter("ParamBodyAngleZ", phase * 3f); // 身体微侧

        // 头略微后仰
        SetParameter("ParamAngleX", phase * (-8f));

        // 眯眼 + 张嘴（打哈欠表情）
        SetParameter("ParamEyeLOpen", 1f - phase * STRETCH_EYE_CLOSE);
        SetParameter("ParamEyeROpen", 1f - phase * STRETCH_EYE_CLOSE);
        SetParameter("ParamMouthForm", phase * STRETCH_MOUTH_OPEN);
        SetParameter("ParamBreath", phase * 0.5f); // 深吸一口气

        // 结束恢复
        if (t >= 1f) ResetIdleAction();
    }

    /// <summary>
    /// 动作6: 爱心眨眼 💕
    /// 爱心眼出现 + 歪头 + 微笑
    /// </summary>
    private void UpdateHeartEyes()
    {
        float p = _complexActionPhase;
        float duration = HEART_DURATION;

        float t = Mathf.Clamp01(p / duration);
        float eased = Mathf.Sin(t * Mathf.PI);

        // 爱心眼闪烁
        float heartPulse = (Mathf.Sin(p * 6f) + 1f) * 0.5f * eased;
        SetParameter("Param109", heartPulse);     // 爱心眼

        // 歪头 + 微笑
        float tiltPulse = Mathf.Sin(p * 2f) * eased;
        SetParameter("ParamAngleZ", tiltPulse * HEART_TILT);
        SetParameter("ParamEyeLSmile", eased * HEART_SMILE);
        SetParameter("ParamEyeRSmile", eased * HEART_SMILE);

        // 身体微倾
        SetParameter("ParamBodyAngleZ", tiltPulse * 4f);

        if (t >= 1f) ResetIdleAction();
    }

    /// <summary>
    /// 动作7: 数钱钱 💰
    /// 双眼放光 + 美滋滋微笑 + 身体微摇
    /// </summary>
    private void UpdateMoney()
    {
        float p = _complexActionPhase;
        float duration = MONEY_DURATION;

        float t = Mathf.Clamp01(p / duration);
        float eased = Mathf.Sin(t * Mathf.PI);

        // 数钱表情（Param122 = 钱）
        SetParameter("Param122", eased);

        // 微笑（美滋滋）
        SetParameter("ParamEyeLSmile", eased * MONEY_SMILE);
        SetParameter("ParamEyeRSmile", eased * MONEY_SMILE);

        // 歪头卖萌
        SetParameter("ParamAngleZ", Mathf.Sin(p * 1.5f) * eased * MONEY_TILT);

        // 身体美滋滋地摇晃
        float sway = Mathf.Sin(p * 2f) * eased * MONEY_SWAY;
        SetParameter("ParamBodyAngleZ", sway);
        SetParameter("ParamBodyAngleX", sway * 0.3f);

        // 眼睛微微眯起（满足感）
        SetParameter("ParamEyeLOpen", 1f - eased * 0.15f);
        SetParameter("ParamEyeROpen", 1f - eased * 0.15f);

        if (t >= 1f) ResetIdleAction();
    }

    /// <summary>
    /// 动作8: 委屈 😢
    /// 泪眼汪汪 + 低头 + 八字眉 + 嘴巴微颤
    /// </summary>
    private void UpdateCry()
    {
        float p = _complexActionPhase;
        float duration = CRY_DURATION;

        float t = Mathf.Clamp01(p / duration);
        float eased = Mathf.Sin(t * Mathf.PI);

        // 泪眼表情（Param130 = 泪眼）
        float tear = Mathf.Sin(p * 4f) * eased;
        float tearFade = Mathf.Clamp01(tear);
        SetParameter("Param130", tearFade);

        // 低头（委屈低头）
        SetParameter("ParamAngleY", eased * CRY_HEAD_DOWN);

        // 嘴巴微颤（委屈时嘴巴会有点抖）
        float mouthTrem = Mathf.Sin(p * 6f) * eased * CRY_MOUTH_TREM;
        SetParameter("ParamMouthForm", Mathf.Abs(mouthTrem));

        // 八字眉（眉头抬起 = 委屈状）
        float browUp = eased * CRY_BROW_UP;
        SetParameter("ParamBrowRY", browUp);   // 右眉抬起
        SetParameter("ParamBrowLY", browUp);   // 左眉抬起

        // 眼睛微微睁大（泪眼汪汪）
        SetParameter("ParamEyeLOpen", 1f + eased * 0.15f);
        SetParameter("ParamEyeROpen", 1f + eased * 0.15f);

        // 身体微微抽动（抽泣感）
        float sob = Mathf.Sin(p * 3.5f) * eased * 1.5f;
        SetParameter("ParamBodyAngleX", sob);
        SetParameter("ParamBodyAngleZ", sob * 0.5f);

        if (t >= 1f) ResetIdleAction();
    }

    /// <summary>
    /// 动作10: 害羞黑脸 😊🖤
    /// 脸黑（Param101）+ 眼神躲闪 + 低头害羞微笑
    /// </summary>
    private void UpdateBlush()
    {
        float p = _complexActionPhase;
        float duration = BLUSH_DURATION;

        float t = Mathf.Clamp01(p / duration);
        float eased = Mathf.Sin(t * Mathf.PI);

        // 黑脸表情（Param101 = 黑脸）
        float darkPulse = (Mathf.Sin(p * 3f) + 1f) * 0.5f * eased;
        SetParameter("Param101", darkPulse * BLUSH_DARK);

        // 害羞低头 + 眼神躲闪
        SetParameter("ParamAngleY", eased * 4f);            // 微低头
        SetParameter("ParamAngleZ", Mathf.Sin(p * 1.5f) * eased * BLUSH_LOOK_AWAY); // 头扭开

        // 害羞微笑
        SetParameter("ParamEyeLSmile", eased * BLUSH_SMILE);
        SetParameter("ParamEyeRSmile", eased * BLUSH_SMILE);

        // 眼睛微微眯起
        SetParameter("ParamEyeLOpen", 1f - eased * 0.2f);
        SetParameter("ParamEyeROpen", 1f - eased * 0.2f);

        // 身体微侧（害羞扭捏）
        float sway = Mathf.Sin(p * 2.5f) * eased * 3f;
        SetParameter("ParamBodyAngleX", sway);
        SetParameter("ParamBodyAngleZ", sway * 0.5f);

        // 嘴巴微张（欲言又止）
        SetParameter("ParamMouthForm", eased * 0.3f);

        if (t >= 1f) ResetIdleAction();
    }

    /// <summary>
    /// 动作11: 困惑 🤔
    /// 歪头+皱眉+眯眼+嘴巴微张 — 像小狗听不懂时歪头那样
    /// 权重为 0，不自发触发，仅当 AI 回复困惑内容时由 AutoChat 强制调用
    /// </summary>
    private void UpdateConfuse()
    {
        float p = _complexActionPhase;
        float duration = CONFUSE_DURATION;

        float t = Mathf.Clamp01(p / duration);
        float eased = Mathf.Sin(t * Mathf.PI);

        // 歪头（经典困惑姿势）
        SetParameter("ParamAngleZ", eased * CONFUSE_TILT);

        // 头微微偏
        SetParameter("ParamAngleX", eased * CONFUSE_HEAD_SIDE);

        // 皱眉（压低眉毛）
        SetParameter("ParamBrowRY", eased * CONFUSE_BROW);
        SetParameter("ParamBrowLY", eased * CONFUSE_BROW);

        // 微微眯眼（困惑地打量）
        SetParameter("ParamEyeLOpen", 1f - eased * CONFUSE_EYE_SQUINT);
        SetParameter("ParamEyeROpen", 1f - eased * CONFUSE_EYE_SQUINT);

        // 嘴巴微张
        SetParameter("ParamMouthForm", eased * CONFUSE_MOUTH);

        // 身体微侧
        SetParameter("ParamBodyAngleZ", eased * CONFUSE_BODY_SIDE);

        if (t >= 1f) ResetIdleAction();
    }

    /// <summary>
    /// 动作9: 法阵显现 ✨
    /// 五阶段：举过头顶→剑指成型→指尖凝光→白光扩散→消散
    /// </summary>
    // ★ Ease 辅助函数
    private float EaseOutQuad(float x) { return 1f - (1f - x) * (1f - x); }
    private float EaseInQuad(float x) { return x * x; }
    private float EaseInCubic(float x) { return x * x * x; }

    private void UpdateMagicCircle()
    {
        float p = _complexActionPhase;
        float duration = CIRCLE_DURATION;
        float t = Mathf.Clamp01(p / duration);

        // ===== 身体姿态（全阶段） =====
        SetParameter("ParamBodyAngleX", -8f);
        SetParameter("ParamAngleX", -10f);
        SetParameter("ParamBodyAngleZ", 0);

        // ===== 手部图层优先级（Phase1-4 保持最前，不被衣服挡住） =====
        if (t < 0.75f) SetHandLayer(1f);

        // ★ Param92 全程保持剑指模式（不随h渐消，防10指重叠）
        SetParameter("Param92", 1f);

        // ===== 仅保留身体姿态和手势，已移除所有视觉特效参数 =====
        if (t < 0.20f)
        {
            float h = EaseInCubic(t / 0.20f);
            SetHandPose(h);
            SetSwordFinger(h);
            return;
        }

        if (t < 0.45f)
        {
            SetHandPose(1f);
            SetSwordFinger(1f);
            return;
        }

        if (t < 0.50f)
        {
            SetHandPose(1f);
            SetSwordFinger(1f);
            return;
        }

        if (t < 0.75f)
        {
            float h = 1f - EaseOutQuad((t - 0.48f) / 0.27f);
            SetHandPose(h);
            SetSwordFinger(h);
            return;
        }

        // ===== Phase5: 消散 =====
        {
            float phase5 = (t - 0.75f) / 0.25f;
            float fade = 1f - EaseOutQuad(phase5);
            SetParameter("ParamBodyAngleX", fade * -8f);
            SetParameter("ParamAngleX", fade * -10f);
            SetHandLayer(fade);
            SetParameter("Param92", fade);
            if (t >= 1f) ResetIdleAction();
        }
    }

    /// <summary>设置剑指手指参数（h=0~1 控制强度，用于淡入淡出，不设Param92防10指重叠）</summary>
    private void SetSwordFinger(float h)
    {
        SetParameter("Param102", 0f);
        SetParameter("Param103", 0f);
        SetParameter("Param105", 0f);
        SetParameter("Param106", 0f);
        SetParameter("Param107", 0f);
        SetParameter("Param111", h * 0.2f);
        SetParameter("Param112", h * 1f);
        SetParameter("Param113", h * 1f);
        SetParameter("Param114", h * 0.2f);
        SetParameter("Param115", h * 0.2f);
        SetParameter("Param110", h * -0.5f);
    }

    /// <summary>设置剑指单手姿势（右手剑指，h=0~1 控制强度，用于淡入淡出）
    /// ★ 左手不抬起（保持自然下垂），符玄法阵是右手单手指天</summary>
    private void SetHandPose(float h)
    {
        // 右手（改为顺时针）
        SetParameter("Param94", h * -4.84f);
        SetParameter("Param97", h * -27.42f);
        SetParameter("Param93", h * 1f); // 右手切换→剑指模式
        SetParameter("Param118", h * -0.32f);
        SetParameter("Param99", h * -18.71f);
        SetParameter("Param38", h * 0f);
        SetParameter("Param39", h * 0f);
        SetParameter("Param31", h * -8f);
        SetParameter("Param32", h * -6f);  // 右臂R2
        SetParameter("Param33", h * -10f); // 右臂R1（上臂）
        // ★ 左手保持0（不下垂也不抬起），维持自然体态
        SetParameter("Param34", 0f);
        SetParameter("Param36", 0f);
        SetParameter("Param37", 0f);
    }

    /// <summary>设置手部透视/图层（独立于姿势，始终在最上层）</summary>
    private void SetHandLayer(float layer)
    {
        SetParameter("Param95", layer * 1f);
        SetParameter("Param117", layer * 0.8f);
        SetParameter("Param98", layer * 0.8f);
        SetParameter("Param100", layer * 0.8f);
        SetParameter("Param116", layer * 0.6f);
        SetParameter("Param120", layer * 1f);
        SetParameter("Param108", layer * 1f);
        SetParameter("Param119", layer * 1f);
    }

    /// <summary>重置空闲动作，清理参数</summary>
    private void ResetIdleAction()
    {
        bool wasLocked = _actionLocked;
        _actionLocked = false;

        _currentIdleAction = 0;
        _idleActionTime = 0f;
        _complexActionPhase = 0f;
        _idleActionCooldown = 1.5f; // 冷却1.5秒防立即重播

        // 清理可能被改过的参数（表情/特殊参数）
        SetParameter("ParamAngleZ", 0f);
        SetParameter("ParamEyeLSmile", 0f);
        SetParameter("ParamEyeRSmile", 0f);
        SetParameter("ParamMouthForm", 0f);
        SetParameter("ParamBrowRY", 0f);
        SetParameter("ParamBrowLY", 0f);
        SetParameter("ParamEyeLOpen", 1f);
        SetParameter("ParamEyeROpen", 1f);
        SetParameter("Param109", 0f); // 爱心眼
        SetParameter("Param94", 0f);  // 右手上臂旋转
        SetParameter("Param97", 0f);  // 右手 基础上臂旋转
        SetParameter("Param95", 0f);  // 右手 基础 上壁透视
        SetParameter("Param117", 0f); // 右手 基础 上壁透视2
        SetParameter("Param98", 0f);  // 右手 基础下壁透视
        SetParameter("Param100", 0f); // 右手 基础手 透视
        SetParameter("Param116", 0f); // 透视2
        SetParameter("Param120", 0f); // 手 向前透视效果
        SetParameter("Param108", 0f); // 右手 基础 图层顺序
        SetParameter("Param119", 0f); // 伸手 图层调整
        SetParameter("Param31", 0f);  // 右手臂R1（前臂）
        SetParameter("Param32", 0f);  // 右手臂R2
        SetParameter("Param33", 0f);  // 右手臂R1（上臂）
        SetParameter("Param93", 0f);  // 右手基础切换
        SetParameter("Param34", 0f);  // 左手手臂L1
        SetParameter("Param36", 0f);  // 左手手臂L2
        SetParameter("Param37", 0f);  // 左手手臂L3
        SetParameter("Param401", 0f); // 外蒙版
        SetParameter("Param104", 0f); // 生气
        SetParameter("Param122", 0f); // 钱（数钱）
        SetParameter("Param130", 0f); // 泪眼（委屈）
        SetParameter("Param101", 0f); // 黑脸（害羞）
        SetParameter("Param92", 0f);  // 右手切换→手指模式 OFF
        SetParameter("Param118", 0f); // 右手伸出参数
        SetParameter("Param99", 0f);  // 手腕Z
        SetParameter("Param110", 0f); // 手指Z旋转
        SetParameter("Param111", 0f); // 手指1(拇指)
        SetParameter("Param112", 0f); // 手指2(食指)
        SetParameter("Param113", 0f); // 手指3(中指)
        SetParameter("Param114", 0f); // 手指4(无名指)
        SetParameter("Param115", 0f); // 手指5(小指)
        // 正常五指清零（防Param92切回普通模式时有残留值）
        SetParameter("Param102", 0f);
        SetParameter("Param103", 0f);
        SetParameter("Param105", 0f);
        SetParameter("Param106", 0f);
        SetParameter("Param107", 0f);
        // 法阵参数（已移除黑幕/白圈/发光等视觉特效参数）
        SetParameter("Param155", 0f); // 镜头X
        SetParameter("Param156", 0f); // 镜头Y
        SetParameter("Param157", 0f); // 人物缩小放大

        if (wasLocked)
        {
            Debug.Log("[Live2DRenderer] 强制动作完成，触发回调");
            OnForcedActionFinished?.Invoke();
        }
    }

    #endregion

    /// <summary>
    /// 强制播放指定空闲动作（被右键菜单调用）
    /// </summary>
    public void ForceIdleAction(int actionId)
    {
        if (!_loaded || _cubismModel == null) return;
        _actionLocked = true;
        _currentIdleAction = actionId;
        _idleActionTime = 0f;
        _complexActionPhase = 0f;

        // ★ 强制动作时，清理可能冲突的调试偏移，防止偏移覆盖动画
        if (debugOffsetEnabled && debugOffsets != null && debugOffsets.Count > 0)
        {
            // 手部/手臂相关参数列表（与法阵/伸懒腰等动作冲突的）
            string[] handParams = new string[]
            {
                "Param33","Param31","Param32","Param94","Param97",
                "Param93","Param118","Param99","Param92",
                "Param95","Param117","Param98","Param100","Param116","Param120",
                "Param108","Param119","Param34","Param36","Param37",
                "Param110","Param111","Param112","Param113","Param114","Param115"
            };
            foreach (var name in handParams)
            {
                if (debugOffsets.ContainsKey(name))
                    debugOffsets.Remove(name);
            }
            if (debugOffsets.Count == 0)
                debugOffsetEnabled = false;
        }

        Debug.Log($"[Live2DRenderer] ▶ 强制动作 #{actionId}（锁定，不被走路覆盖）");
    }

    /// <summary>
    /// 按权重随机选取一个空闲动作（1-11），受时间/天气调节
    /// </summary>
    private int PickWeightedIdleAction()
    {
        // 从基值复制，然后根据昼夜/天气调节
        int[] w = new int[_idleActionWeights.Length];
        for (int i = 0; i < w.Length; i++) w[i] = _idleActionWeights[i];

        // ★ 夜间/犯困时段 → 活跃动作减少，犯困/委屈增加
        bool isNight = (_timeController != null && _timeController.isNight);
        bool isSleepy = (_timeController != null && _timeController.isSleepyTime);
        if (isNight)
        {
            w[3] = Mathf.Max(1, w[3] - 1);  // 动作4 星辉
            w[5] = Mathf.Max(1, w[5] - 1);  // 动作6 爱心眼
            w[7] = w[7] + 1;                // 动作8 委屈/困
        }
        if (isSleepy)
        {
            w[7] = w[7] + 2;                // 动作8 更想睡
            w[0] = w[0] + 1;                // 动作1 歪头（没精神歪着）
        }

        // ★ 天气调节
        if (_timeController != null && _timeController.weatherFetched)
        {
            var wt = _timeController.weather;
            if (wt == TimeWeatherController.WeatherType.Rain ||
                wt == TimeWeatherController.WeatherType.Drizzle ||
                wt == TimeWeatherController.WeatherType.Thunder)
            {
                w[7] = w[7] + 2;            // 动作8 委屈（下雨天不开心）
                w[5] = Mathf.Max(1, w[5] - 1); // 动作6 爱心眼减少
                w[6] = Mathf.Max(1, w[6] - 1); // 动作7 数钱减少
            }
            else if (wt == TimeWeatherController.WeatherType.Clear ||
                     wt == TimeWeatherController.WeatherType.Cloudy)
            {
                w[5] = w[5] + 1;            // 动作6 爱心眼（晴天开心）
                w[3] = w[3] + 1;            // 动作4 星辉
            }
            else if (wt == TimeWeatherController.WeatherType.Snow)
            {
                w[2] = w[2] + 1;            // 动作3 挑眉（好奇看雪）
                w[7] = w[7] + 1;            // 动作8 委屈（冷）
            }
        }

        int totalWeight = 0;
        for (int i = 0; i < w.Length; i++)
            totalWeight += w[i];

        int roll = Random.Range(0, totalWeight);
        int cumulative = 0;
        for (int i = 0; i < w.Length; i++)
        {
            cumulative += w[i];
            if (roll < cumulative)
                return i + 1; // 动作编号从 1 开始
        }
        return 1; // fallback
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
        float worldY = _pet.petY + _pet.petHeight / 2f + verticalOffset - _walkBounceOffset;

        Vector3 screenPos = new Vector3(worldX, Screen.height - worldY, 10f);
        Vector3 worldPos = cam.ScreenToWorldPoint(screenPos);

        // 调试日志
        if (Time.frameCount % 60 == 0) // 每秒打印一次
        {
            Debug.Log($"[Live2DRenderer] pet({_pet.petX},{_pet.petY}) size({_pet.petWidth},{_pet.petHeight}) → screenPos({screenPos.x},{screenPos.y}) → worldPos({worldPos.x},{worldPos.y}), scale={modelScale}");
        }

        _modelRoot.transform.position = worldPos;

        // 根据朝向翻转（直接设置scale，不基于当前值）
        bool faceRight = _pet.petVx >= 0;
        Vector3 scale = new Vector3(modelScale, modelScale, 1f);
        // 拖拽中不翻转 scale.x（用 ParamBodyAngleY 平滑转身，避免鼠标微晃时模型 180° 弹跳）
        if (!_pet.isDragging)
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

    /// <summary>
    /// 走路动画 — LateUpdate 中调用，相位已同步
    /// 
    /// ★ 侧面走路（横版过关风格）
    ///   用 ParamBodyAngleY 转体，观众能清楚看到抬腿和摆臂
    ///   身体稳定（恒定转体+前倾），腿/臂交替运动产生走路信号
    /// </summary>
    /// <summary>
    /// 走路手臂/腿摆动动画
    /// </summary>
    /// <param name="blendWeight">混合权重：1=全幅度走路，0=不设任何值（默认=1）</param>
    private void UpdateWalkAnimation(float blendWeight = 1f)
    {
        if (blendWeight <= 0f) return;
        float phase = _walkPhase;

        // ★ 腿/臂动态摆动 — 停止后渐消不需要这些
        // ★ 左腿参数
        //   左腿向前(+)时，右手臂也向前(+) — 交叉对位
        float legPhase = Mathf.Sin(phase);
        float rightPhase = -legPhase; // 右腿与左腿反相

        // 抬腿（已乘 blendWeight 消退）
        SetParameter("Param165", legPhase * WALK_LEG_LIFT * blendWeight);
        SetParameter("Param164", rightPhase * WALK_LEG_LIFT * blendWeight);

        // 前后摆动 + 弯曲
        SetParameter("Param126", legPhase * WALK_LEG_SWING * blendWeight);
        SetParameter("Param127", Mathf.Abs(legPhase) * WALK_LEG_BEND * blendWeight);

        // 右腿
        SetParameter("Param129", rightPhase * WALK_LEG_SWING * blendWeight);
        SetParameter("Param131", Mathf.Abs(rightPhase) * WALK_LEG_BEND * blendWeight);

        // ★ 右手臂与左腿同步（交叉对位：左腿前→右手前）
        //   左臂与右腿同步（右腿前→左手前），与右臂反相
        SetParameter("Param94", legPhase * WALK_ARM_BIG * blendWeight);          // 右臂 上臂旋转 (大范围)
        SetParameter("Param31", legPhase * WALK_ARM_SMALL * 0.7f * blendWeight); // 右臂R1
        SetParameter("Param32", legPhase * WALK_ARM_SMALL * 0.4f * blendWeight); // 右臂R2
        SetParameter("Param33", legPhase * WALK_ARM_SMALL * 0.4f * blendWeight); // 右臂R1上臂
        float leftArm = rightPhase * WALK_ARM_SMALL * blendWeight;
        SetParameter("Param34", leftArm * 0.7f);  // 左臂L1
        SetParameter("Param36", leftArm * 0.4f);  // 左臂L2
        SetParameter("Param37", leftArm * 0.4f);  // 左臂L3

        // 肩膀配合脚步
        SetParameter("Param153", Mathf.Abs(legPhase) * WALK_SHOULDER * blendWeight);
    }

    /// <summary>
    /// 走路的体态姿势（转体 + 前倾 + 低头 + 呼吸加深）
    /// 用 weight 控制消退：1=全走路态，0=完全消失
    /// </summary>
    private void ApplyWalkBodyPose(float weight)
    {
        if (weight <= 0f) return;

        float phase = _walkPhase;

        // 身体转体侧面
        float bodyYaw = (WALK_SIDE_ANGLE + Mathf.Sin(phase) * 3f) * weight;
        SetParameter("ParamBodyAngleY", bodyYaw);

        // 身体前倾
        SetParameter("ParamBodyAngleX", WALK_BODY_LEAN * weight);

        // 身体左右横摆（驱动衣服物理）
        float bodySwing = Mathf.Sin(phase) * WALK_BODY_SWING * weight;
        SetParameter("ParamBodyAngleZ", bodySwing);

        // 脸转向侧面（与身体方向一致）
        SetParameter("ParamAngleX", WALK_SIDE_ANGLE * weight);

        // 头微低看路
        SetParameter("ParamAngleY", WALK_HEAD_TILT * weight);

        // 呼吸加深
        SetParameter("ParamBreath", (WALK_BREATH + Mathf.Sin(phase) * 0.5f) * weight);
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
        // 记录当前帧位置，重置速度追踪（新拖拽从零开始）
        _lastDragPetX = _pet != null ? _pet.petX : 0;
        _dragSmoothBodyZ = 0f;
        _dragSmoothHeadX = 0f;
        _dragSmoothHeadZ = 0f;
        _dragInited = true;
    }

    public void ShowClickPose(float hitNormY)
    {
        _clickSavedParams.Clear();

        if (hitNormY <= 0.35f)
        {
            // === 头部 → 摸头：眯眼歪头 ===
            _clickSavedParams["ParamEyeLOpen"] = CLICK_EYE_OPEN;
            _clickSavedParams["ParamEyeROpen"] = CLICK_EYE_OPEN;
            _clickSavedParams["ParamAngleX"] = CLICK_HEAD_ANGLE_X;
            _clickSavedParams["ParamBodyAngleX"] = CLICK_BODY_ANGLE_X;
        }
        else if (hitNormY <= 0.65f)
        {
            // === 身体 → 戳一下：睁大眼张嘴惊讶 ===
            _clickSavedParams["ParamEyeLOpen"] = POKE_EYE_OPEN;
            _clickSavedParams["ParamEyeROpen"] = POKE_EYE_OPEN;
            _clickSavedParams["ParamMouthOpenY"] = POKE_MOUTH_OPEN;
            _clickSavedParams["ParamMouthForm"] = POKE_MOUTH_FORM;
            _clickSavedParams["ParamBrowLY"] = POKE_BROW_RAISE;
            _clickSavedParams["ParamBrowRY"] = POKE_BROW_RAISE;
            _clickSavedParams["ParamAngleX"] = CLICK_HEAD_ANGLE_X * 0.5f;
        }
        else
        {
            // === 腿/脚 → 碰腿：害羞开心 ===
            _clickSavedParams["ParamEyeLOpen"] = LEG_HIT_EYE_CLOSE;
            _clickSavedParams["ParamEyeROpen"] = LEG_HIT_EYE_CLOSE;
            _clickSavedParams["ParamEyeLSmile"] = LEG_HIT_SMILE;
            _clickSavedParams["ParamEyeRSmile"] = LEG_HIT_SMILE;
            _clickSavedParams["ParamAngleZ"] = LEG_HIT_ANGLE_Z;
            _clickSavedParams["ParamAngleX"] = CLICK_HEAD_ANGLE_X * 0.3f;
            _clickSavedParams["ParamBodyAngleX"] = CLICK_BODY_ANGLE_X * 0.5f;
        }

        // 立即应用一次
        foreach (var kv in _clickSavedParams)
            SetParameter(kv.Key, kv.Value);

        _poseLocked = true;
        _poseLockUntil = Time.time + CLICK_LOCK_TIME;
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

    public void ShowWallHitPose(int direction)
    {
        // 开启反弹动画计时器
        _wallHitTime = WALL_HIT_DURATION;

        // 瞪眼（瞬时覆盖，后续由 LateUpdate 衰减）
        SetParameter("ParamEyeLOpen", WALL_HIT_EYE_OPEN);
        SetParameter("ParamEyeROpen", WALL_HIT_EYE_OPEN);
        SetParameter("ParamMouthOpenY", WALL_HIT_MOUTH_OPEN);

        // 身体往反方向倾斜（受惊后仰）
        float bodyLean = (direction > 0) ? -WALL_HIT_BODY_LEAN : WALL_HIT_BODY_LEAN;
        SetParameter("ParamBodyAngleX", bodyLean);

        Debug.Log($"[Live2DRenderer] 墙碰! direction={direction}");
    }

    public void SetEyeTarget(float? targetX, float? targetY)
    {
        _eyeTargetX = targetX;
        _eyeTargetY = targetY;
    }

    public void OnPetUpdate(int petX, int petY, int petWidth, int petHeight,
                            int petVx, int petVy, bool onGround, bool isDragging, bool isPaused)
    {
        if (!_loaded || _cubismModel == null) return;

        if (isDragging)
        {
            // ⭐4 拖拽挣扎（LateUpdate 中也会调用以覆盖物理）
            UpdateDragStruggle();
            if (_cubismModel != null) _cubismModel.ForceUpdateNow();
            return;
        }

        if (_poseLocked && Time.time < _poseLockUntil) return;
        _poseLocked = false;

        if (!onGround && petVy > 0)
        {
            // 下落 — 身体前倾
            SetParameter("ParamBodyAngleX", FALL_BODY_ANGLE_X);
            SetParameter("ParamAngleX", FALL_HEAD_ANGLE_X);
            SetParameter("ParamBreath", 0f);
        }
        else if (onGround)
        {
            // 走路参数统一在 LateUpdate 中设置（确保相位同步）
            // 这里什么都不做，避免 Update 执行顺序问题
        }
    }

    #endregion

    /// <summary>
    /// ⭐4 拖拽挣扎动画 — 手脚交替划水 + 身体扭动 + 慌张表情
    /// 从 OnPetUpdate 抽出，供 LateUpdate 在物理之后重新覆盖
    /// </summary>
    private void UpdateDragStruggle()
    {
        float t = Time.time;

        // 初始化拖拽速度追踪（防第一帧跳变）
        if (!_dragInited)
        {
            _lastDragPetX = _pet != null ? _pet.petX : 0;
            _dragSmoothBodyZ = 0f;
            _dragSmoothHeadX = 0f;
            _dragSmoothHeadZ = 0f;
            _dragInited = true;
        }

        // === 双臂交替挣扎（模型坐标系：右臂正=向前，左臂负=向前）===
        float phase = t * DRAG_ARM_FREQ;
        float swing = Mathf.Sin(phase);
        float jitter = Mathf.Sin(t * DRAG_JITTER1_FREQ) * DRAG_JITTER1_AMP
                     + Mathf.Sin(t * DRAG_JITTER2_FREQ) * DRAG_JITTER2_AMP;
        float rightBase = swing * DRAG_RIGHT_AMP * (1f + jitter);
        // 左臂同相位（模型坐标系下右正=向前，左负=向前，同相位=真正交替）
        float leftBase = swing * DRAG_LEFT_AMP;

        // 右臂关节
        float rMag = Mathf.Clamp01((rightBase + DRAG_RIGHT_AMP) / (DRAG_RIGHT_AMP * 2f));
        SetParameter("Param94", rightBase * DRAG_RPARAM94);
        SetParameter("Param97", rightBase * DRAG_RPARAM97);
        SetParameter("Param31", rightBase * DRAG_RPARAM31);
        SetParameter("Param32", rightBase * DRAG_RPARAM32);
        SetParameter("Param33", rightBase * DRAG_RPARAM33);
        SetParameter("Param93", rMag * DRAG_RPARAM93);
        SetParameter("Param118", rMag * DRAG_RPARAM118);
        // 右手透视图层跟随幅度
        SetParameter("Param95", rMag * DRAG_LAYER95);
        SetParameter("Param117", rMag * DRAG_LAYER117);
        SetParameter("Param98", rMag * DRAG_LAYER98);
        SetParameter("Param100", rMag * DRAG_LAYER100);
        SetParameter("Param116", rMag * DRAG_LAYER116);
        SetParameter("Param120", rMag * DRAG_LAYER120);
        SetParameter("Param108", rMag * DRAG_LAYER108);
        SetParameter("Param119", rMag * DRAG_LAYER119);

        // 左臂
        SetParameter("Param34", leftBase * DRAG_LPARAM34);
        SetParameter("Param36", leftBase * DRAG_LPARAM36);
        SetParameter("Param37", leftBase * DRAG_LPARAM37);

        // 双腿交替
        float legPhase = t * DRAG_LEG_FREQ;
        float legSwing = Mathf.Sin(legPhase);
        float rightLeg = -legSwing;
        SetParameter("Param126", legSwing * DRAG_LEG_SWING);
        SetParameter("Param127", Mathf.Abs(legSwing) * DRAG_LEG_BEND);
        SetParameter("Param129", rightLeg * DRAG_LEG_SWING);
        SetParameter("Param131", Mathf.Abs(rightLeg) * DRAG_LEG_BEND);
        SetParameter("Param165", legSwing * DRAG_LEG_LIFT);
        SetParameter("Param164", rightLeg * DRAG_LEG_LIFT);

        // 身体晃动（带平滑转身：鼠标方向决定 ParamBodyAngleY，不做 scale.x 硬翻转）
        float targetBodyY = _pet != null ? (_pet.petVx > 0 ? DRAG_TURN_ANGLE : -DRAG_TURN_ANGLE) : 0f;
        _dragSmoothBodyY = Mathf.Lerp(_dragSmoothBodyY, targetBodyY, DRAG_TURN_SMOOTH);
        SetParameter("ParamBodyAngleY", _dragSmoothBodyY);

        // ★ 帧间速度 → 输入参数 + 直接驱动裙子/法盘（全部同方向）
        float rawVel = _pet != null ? (_pet.petX - _lastDragPetX) : 0f;
        _lastDragPetX = _pet != null ? _pet.petX : 0;
        rawVel = Mathf.Clamp(rawVel, -DRAG_VEL_MAX, DRAG_VEL_MAX); // ← 限幅防瞬冲

        // 平滑滤波
        _dragSmoothBodyZ = Mathf.Lerp(_dragSmoothBodyZ, rawVel, DRAG_VEL_LERP);
        _dragSmoothHeadX = Mathf.Lerp(_dragSmoothHeadX, rawVel, DRAG_VEL_LERP);
        _dragSmoothHeadZ = Mathf.Lerp(_dragSmoothHeadZ, rawVel, DRAG_VEL_LERP);

        float v = _dragSmoothBodyZ;

        // ---- 输入参数（给物理系统，聊胜于无）----
        float bodyZ = Mathf.Clamp(-v * DRAG_BODY_Z_SCALE, -DRAG_BODY_Z_MAX, DRAG_BODY_Z_MAX);
        SetParameter("ParamBodyAngleZ", bodyZ);
        SetParameter("ParamBodyAngleX", Mathf.Sin(t * DRAG_BODY_FREQ) * DRAG_BODY_SWAY);

        // ---- 直接驱动裙子/法盘/帘子（与鼠标方向相反）----
        float d = Mathf.Clamp(-v * DRAG_DIRECT_SCALE, -DRAG_DIRECT_MAX, DRAG_DIRECT_MAX);
        SetParameter("Param82", d);
        SetParameter("Param87", d);
        SetParameter("Param84", d * 0.6f);
        SetParameter("Param49", d);
        SetParameter("Param51", d);
        SetParameter("Param57", d);
        SetParameter("Param60", d);

        // ---- 直接驱动头发（物理 Delay 太高，同方向驱动）----
        float h = Mathf.Clamp(-v * DRAG_HAIR_SCALE, -DRAG_HAIR_MAX, DRAG_HAIR_MAX);
        // 刘海
        SetParameter("Param5", h);
        SetParameter("Param7", h);
        SetParameter("Param9", h);
        // 头发物理2
        SetParameter("Param11", h);
        SetParameter("Param14", h);
        SetParameter("Param17", h);
        // 后发B
        SetParameter("Param19", h);
        SetParameter("Param21", h);
        // 鬓发
        SetParameter("Param23", h);
        SetParameter("Param35", h);
        SetParameter("Param41", h);
        // 后发
        SetParameter("Param43", h);
        SetParameter("Param45", h);
        SetParameter("Param55", h);
        SetParameter("Param62", h);
        // 饰品
        SetParameter("Param91", h);
        SetParameter("Param74", h);
        SetParameter("Param89", h);
        // 头饰（Param169 单独小幅度）
        float h169 = Mathf.Clamp(-v * DRAG_HAIR169_SCALE, -DRAG_HAIR169_MAX, DRAG_HAIR169_MAX);
        SetParameter("Param169", h169);

        float headX = Mathf.Clamp(-v * DRAG_HEAD_X_SCALE, -DRAG_HEAD_X_MAX, DRAG_HEAD_X_MAX);
        float headXShake = headX + Mathf.Sin(t * DRAG_HEAD_SHAKE_FREQ) * DRAG_HEAD_SHAKE;
        SetParameter("ParamAngleX", headXShake);
        float headZ = Mathf.Clamp(v * DRAG_HEAD_Z_SCALE, -DRAG_HEAD_Z_MAX, DRAG_HEAD_Z_MAX);
        SetParameter("ParamAngleZ", headZ);
        SetParameter("ParamAngleY", DRAG_HEAD_TILT + Mathf.Sin(t * DRAG_HEAD_BOB_FREQ) * DRAG_HEAD_BOB);

        // 表情
        SetParameter("ParamEyeLOpen", DRAG_EYE_OPEN);
        SetParameter("ParamEyeROpen", DRAG_EYE_OPEN);
        SetParameter("ParamMouthOpenY", DRAG_MOUTH_AMP + Mathf.Sin(t * DRAG_MOUTH_FREQ + DRAG_MOUTH_PHASE) * DRAG_MOUTH_PULSE);
        SetParameter("ParamBrowL", DRAG_BROW);
        SetParameter("ParamBrowR", DRAG_BROW);
    }

    private void ListAllChildren(GameObject go, int depth)
    {
        string indent = new string(' ', depth * 2);
        Debug.Log($"{indent}{go.name} (active={go.activeInHierarchy})");

        foreach (var component in go.GetComponents<Component>())
        {
            if (component == null) continue;
            if (component is Transform) continue;
            Debug.Log($"{indent}  [{component.GetType().Name}]");
        }

        foreach (Transform child in go.transform)
        {
            ListAllChildren(child.gameObject, depth + 1);
        }
    }

    private void SetLayerRecursively(GameObject go, int layer)
    {
        if (go == null) return;
        go.layer = layer;
        foreach (Transform child in go.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    /// <summary>
    /// 设置模型透明度（HybridRenderer 交叉淡入淡出用）
    /// </summary>
    public void SetAlpha(float alpha)
    {
        if (_modelRoot == null) return;
        var renderers = _modelRoot.GetComponentsInChildren<Renderer>(true);
        foreach (var r in renderers)
        {
            // Live2D Cubism shader 没有 _Color 属性，跳过
            if (!r.material.HasProperty("_Color")) continue;
            Color c = r.material.color;
            c.a = Mathf.Clamp01(alpha);
            r.material.color = c;
        }
    }

    // ===== 公开接口（供 ContextMenu 调试用） =====

    /// <summary>当前播放的随机动作 ID（0=无）</summary>
    public int CurrentActionId => _currentIdleAction;

    /// <summary>是否被强制动作锁定</summary>
    public bool IsActionLocked => _actionLocked;

    /// <summary>设置参数值（公开版）</summary>
    public void SetParameterValue(string name, float value)
    {
        SetParameter(name, value);
    }

    /// <summary>获取参数当前值，失败返回 0</summary>
    public float GetParameterValue(string name)
    {
        if (_cubismModel == null) return 0f;
        var param = _cubismModel.Parameters.FindById(name);
        return param != null ? param.Value : 0f;
    }

    /// <summary>获取参数最小值，失败返回 0</summary>
    public float GetParameterMin(string name)
    {
        if (_cubismModel == null) return 0f;
        var param = _cubismModel.Parameters.FindById(name);
        return param != null ? param.MinimumValue : 0f;
    }

    /// <summary>获取参数最大值，失败返回 0</summary>
    public float GetParameterMax(string name)
    {
        if (_cubismModel == null) return 0f;
        var param = _cubismModel.Parameters.FindById(name);
        return param != null ? param.MaximumValue : 0f;
    }

    /// <summary>获取所有参数名称列表</summary>
    public string[] GetAllParameterNames()
    {
        if (_cubismModel == null) return System.Array.Empty<string>();
        var names = new string[_cubismModel.Parameters.Length];
        for (int i = 0; i < names.Length; i++)
            names[i] = _cubismModel.Parameters[i].Id;
        return names;
    }
}
