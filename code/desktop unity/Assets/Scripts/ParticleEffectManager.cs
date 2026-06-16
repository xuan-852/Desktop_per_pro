using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// 粒子特效管理器 — 纯 OnGUI 绘制，无需 Unity ParticleSystem
///
/// 粒子类型：
/// • Heart ❤ — 双击爱心
/// • Star ✦ — 点击星星
/// • Trail · — 走路拖尾
/// • Petal ✿ — 动作触发花瓣
/// • Sparkle ✧ — 点击小闪光
///
/// 执行顺序：Late GUI（在默认 OnGUI 之后绘制，确保显示在最上层）
/// </summary>
[DefaultExecutionOrder(5000)]
public class ParticleEffectManager : MonoBehaviour
{
    [Header("粒子池")]
    public int maxParticles = 200;

    [Header("爱心")]
    public float heartSize = 40f;
    public float heartLifetime = 1.5f;
    public float heartSpeed = 60f;

    [Header("星星")]
    public float starSize = 24f;
    public float starLifetime = 0.8f;
    public float starSpeed = 80f;

    [Header("拖尾")]
    public float trailSize = 16f;
    public float trailLifetime = 0.6f;
    public float trailInterval = 0.08f;

    [Header("花瓣")]
    public float petalSize = 20f;
    public float petalLifetime = 2f;

    // ===== 粒子定义 =====
    private struct Particle
    {
        public Vector2 pos;
        public Vector2 vel;
        public float life;       // 剩余时间
        public float maxLife;    // 总时间
        public float size;
        public Color color;
        public int type;         // 0=heart, 1=star, 2=trail, 3=petal, 4=sparkle
        public float rotation;
        public float rotSpeed;
    }

    private List<Particle> _particles = new List<Particle>();
    private float _trailTimer = 0f;

    // ===== 纹理（运行时生成） =====
    private Texture2D _circleTex;   // 模糊圆（拖尾/闪光）
    private Texture2D _starTex;     // 星星
    private bool _texReady = false;

    // ===== 颜色主题（符玄紫） =====
    private static readonly Color COLOR_HEART   = new Color(1f, 0.3f, 0.5f, 1f);     // 粉红
    private static readonly Color COLOR_STAR    = new Color(0.8f, 0.5f, 1f, 1f);     // 淡紫
    private static readonly Color COLOR_TRAIL   = new Color(0.6f, 0.3f, 0.8f, 0.8f); // 紫半透明
    private static readonly Color COLOR_PETAL   = new Color(1f, 0.7f, 0.9f, 0.85f);  // 粉瓣
    private static readonly Color COLOR_SPARKLE = new Color(1f, 0.9f, 0.6f, 0.85f);  // 金色

    void Start()
    {
        GenerateTextures();

        // ★ 测试：生成超大粒子，确认粒子系统可见
        Vector2 testPos = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
        for (int i = 0; i < 10; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            _particles.Add(new Particle
            {
                pos = testPos,
                vel = new Vector2(Mathf.Cos(angle) * 30f, Mathf.Sin(angle) * 30f),
                life = 5f,
                maxLife = 5f,
                size = 60f,
                color = new Color(1f, 0f, 0f, 1f),
                type = 2,
                rotation = 0f,
                rotSpeed = 0f
            });
        }
        Debug.Log($"[ParticleEffectManager] 启动，屏幕={Screen.width}x{Screen.height}，生成10个红色大测试粒子 @ {testPos}");
    }

    private void GenerateTextures()
    {
        if (_texReady) return;

        // 圆形（硬边缘 + 径向渐变）
        int r = 16;
        _circleTex = new Texture2D(r * 2, r * 2, TextureFormat.ARGB32, false);
        for (int y = 0; y < r * 2; y++)
        {
            for (int x = 0; x < r * 2; x++)
            {
                float dx = (x - r + 0.5f) / r;
                float dy = (y - r + 0.5f) / r;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float alpha = Mathf.Clamp01(1f - dist);
                // 硬边：内圈全白，外圈渐变
                if (dist < 0.7f)
                    _circleTex.SetPixel(x, y, new Color(1, 1, 1, 1f));
                else
                    _circleTex.SetPixel(x, y, new Color(1, 1, 1, alpha * alpha));
            }
        }
        _circleTex.filterMode = FilterMode.Bilinear;
        _circleTex.wrapMode = TextureWrapMode.Clamp;
        _circleTex.Apply();

        // 星星（菱形 + 十字）
        int s = 16;
        _starTex = new Texture2D(s * 2, s * 2, TextureFormat.ARGB32, false);
        for (int y = 0; y < s * 2; y++)
        {
            for (int x = 0; x < s * 2; x++)
            {
                float dx = (x - s + 0.5f) / s;
                float dy = (y - s + 0.5f) / s;
                float dist = Mathf.Sqrt(dx * dx + dy * dy);
                float ang = Mathf.Atan2(dy, dx);
                // 4角星形
                float starShape = Mathf.Abs(Mathf.Cos(ang * 4f));
                float alpha = Mathf.Clamp01((1f - dist) * (0.5f + 0.5f * starShape));
                alpha = Mathf.Clamp01(alpha * 1.5f); // 整体提亮
                _starTex.SetPixel(x, y, new Color(1, 1, 1, alpha));
            }
        }
        _starTex.filterMode = FilterMode.Bilinear;
        _starTex.wrapMode = TextureWrapMode.Clamp;
        _starTex.Apply();

        _texReady = true;
    }

    void Update()
    {
        // 更新所有粒子
        for (int i = _particles.Count - 1; i >= 0; i--)
        {
            var p = _particles[i];
            p.life -= Time.deltaTime;
            if (p.life <= 0f)
            {
                _particles.RemoveAt(i);
                continue;
            }

            // 物理
            p.pos += p.vel * Time.deltaTime;
            p.vel *= 0.97f; // 阻力
            p.vel.y += 20f * Time.deltaTime; // 微重力（向上为正）
            p.rotation += p.rotSpeed * Time.deltaTime;

            _particles[i] = p;
        }
    }

    // ================================================================
    //  公开接口
    // ================================================================

    /// <summary>双击爱心 — 在指定位置爆出多个爱心</summary>
    public void BurstHearts(Vector2 pos, int count = 6)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(heartSpeed * 0.5f, heartSpeed * 1.2f);
            SpawnParticle(new Particle
            {
                pos = pos,
                vel = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed - 40f),
                life = heartLifetime * Random.Range(0.7f, 1.3f),
                maxLife = heartLifetime,
                size = heartSize * Random.Range(0.7f, 1.3f),
                color = COLOR_HEART,
                type = 0,
                rotation = Random.Range(0f, 360f),
                rotSpeed = Random.Range(-90f, 90f)
            });
        }
    }

    /// <summary>点击星星 — 在点击位置爆出小星星</summary>
    public void BurstStars(Vector2 pos, int count = 5)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(0f, Mathf.PI * 2f);
            float speed = Random.Range(starSpeed * 0.3f, starSpeed);
            SpawnParticle(new Particle
            {
                pos = pos,
                vel = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed),
                life = starLifetime * Random.Range(0.5f, 1.2f),
                maxLife = starLifetime,
                size = starSize * Random.Range(0.5f, 1.5f),
                color = Random.Range(0f, 1f) > 0.5f ? COLOR_STAR : COLOR_SPARKLE,
                type = 1,
                rotation = Random.Range(0f, 360f),
                rotSpeed = Random.Range(-180f, 180f)
            });
        }
    }

    /// <summary>走路拖尾 — 在宠物脚底生成一串小光点</summary>
    public void WalkingTrail(Vector2 petPos, float petWidth, float petHeight, bool facingRight)
    {
        _trailTimer += Time.deltaTime;
        if (_trailTimer < trailInterval) return;
        _trailTimer = 0f;

        float offsetX = facingRight ? -petWidth * 0.1f : petWidth * 0.1f;
        Vector2 trailPos = new Vector2(
            petPos.x + petWidth * 0.5f + offsetX,
            petPos.y + petHeight * 0.15f
        );

        SpawnParticle(new Particle
        {
            pos = trailPos + Random.insideUnitCircle * 3f,
            vel = new Vector2(Random.Range(-5f, 5f), Random.Range(-8f, -2f)),
            life = trailLifetime * Random.Range(0.6f, 1.2f),
            maxLife = trailLifetime,
            size = trailSize * Random.Range(0.5f, 1.5f),
            color = COLOR_TRAIL,
            type = 2,
            rotation = 0f,
            rotSpeed = 0f
        });
    }

    /// <summary>花瓣飘落 — 动作触发时飘落一圈花瓣</summary>
    public void BurstPetals(Vector2 pos, int count = 8)
    {
        for (int i = 0; i < count; i++)
        {
            float angle = Random.Range(-Mathf.PI, 0f); // 朝上方向
            float speed = Random.Range(petalSpeed * 0.5f, petalSpeed);
            SpawnParticle(new Particle
            {
                pos = pos + Random.insideUnitCircle * 10f,
                vel = new Vector2(Mathf.Cos(angle) * speed, Mathf.Sin(angle) * speed),
                life = petalLifetime * Random.Range(0.7f, 1.3f),
                maxLife = petalLifetime,
                size = petalSize * Random.Range(0.6f, 1.4f),
                color = COLOR_PETAL,
                type = 3,
                rotation = Random.Range(0f, 360f),
                rotSpeed = Random.Range(-60f, 60f)
            });
        }
    }

    private float petalSpeed = 40f;

    /// <summary>清空所有粒子</summary>
    public void ClearAll()
    {
        if (_particles.Count > 0)
        {
            Debug.Log($"[ParticleEffectManager] 清空 {_particles.Count} 个粒子");
            _particles.Clear();
        }
    }

    // ================================================================
    //  内部
    // ================================================================

    private void SpawnParticle(Particle p)
    {
        if (_particles.Count >= maxParticles)
            _particles.RemoveAt(0);
        _particles.Add(p);
        Debug.Log($"[ParticleEffectManager] 生成粒子 type={p.type} @ ({p.pos.x:F0},{p.pos.y:F0}) size={p.size:F1} life={p.life:F2} 池={_particles.Count}");
    }

    void OnGUI()
    {
        // ★★★ 调试标记：确认 OnGUI 被执行（屏幕上固定显示一个大色块）
        GUI.color = new Color(1f, 1f, 0f, 0.5f);
        GUI.DrawTexture(new Rect(Screen.width - 60, 10, 40, 40), _circleTex ?? Texture2D.whiteTexture);
        GUI.color = Color.white;
        GUI.Label(new Rect(Screen.width - 60, 55, 120, 20), "[粒子系统]");

        if (_particles.Count == 0) return;

        // ★ 只在 Repaint 事件绘制，避免 Layout/Event 等事件触发矩阵操作
        if (Event.current.type != EventType.Repaint) return;

        // 纹理未就绪时跳过
        if (!_texReady || _circleTex == null || _starTex == null)
        {
            Debug.LogWarning($"[ParticleEffectManager] 纹理未就绪，跳过渲染 (texReady={_texReady}, circle={_circleTex!=null}, star={_starTex!=null})");
            return;
        }

        int drawn = 0;
        foreach (var p in _particles)
        {
            float t = p.life / p.maxLife; // 1→0
            float alpha = Mathf.Clamp01(t < 0.3f ? t / 0.3f : 1f); // 最后30%渐隐
            float s = p.size * (0.8f + 0.2f * t); // 缓慢缩小

            Color c = p.color;
            c.a *= alpha;

            // ★ 跳过完全透明
            if (c.a < 0.01f) continue;

            GUI.color = c;

            Rect r = new Rect(p.pos.x - s / 2f, p.pos.y - s / 2f, s, s);

            switch (p.type)
            {
                case 0: // Heart
                    DrawHeart(p.pos, s);
                    break;
                case 1: // Star
                    DrawRotated(r, _starTex, p.rotation);
                    break;
                case 2: // Trail
                    GUI.DrawTexture(r, _circleTex);
                    break;
                case 3: // Petal
                    DrawPetal(p.pos, s, p.rotation);
                    break;
                case 4: // Sparkle
                    DrawRotated(r, _starTex, p.rotation * 3f);
                    break;
            }
            drawn++;
        }

        GUI.color = Color.white;

        if (drawn > 0 && drawn % 50 == 0)
            Debug.Log($"[ParticleEffectManager] 渲染了 {drawn}/{_particles.Count} 粒子");
    }

    private void DrawHeart(Vector2 pos, float size)
    {
        if (_circleTex == null) return;

        float half = size * 0.5f;
        float radius = size * 0.33f;

        // 用三个圆形拼出心形
        // 左圆
        GUI.DrawTexture(new Rect(pos.x - half, pos.y - half, radius * 2, radius * 2), _circleTex);
        // 右圆
        GUI.DrawTexture(new Rect(pos.x - half + radius, pos.y - half, radius * 2, radius * 2), _circleTex);
        // 底部三角区域
        GUI.DrawTexture(new Rect(pos.x - half, pos.y - half + radius * 0.5f, radius * 2, radius * 2), _circleTex);
        GUI.DrawTexture(new Rect(pos.x - half + radius, pos.y - half + radius * 0.5f, radius * 2, radius * 2), _circleTex);
    }

    private void DrawPetal(Vector2 pos, float size, float rot)
    {
        if (_circleTex == null) return;
        Rect r = new Rect(pos.x - size / 2f, pos.y - size / 2f, size, size);
        GUI.DrawTexture(r, _circleTex);
        // 叠加小圆增强
        GUI.DrawTexture(new Rect(r.x + r.width * 0.2f, r.y + r.height * 0.2f,
            r.width * 0.6f, r.height * 0.6f), _circleTex);
    }

    private void DrawRotated(Rect r, Texture2D tex, float angle)
    {
        if (tex == null) return;
        Matrix4x4 orig = GUI.matrix;
        GUIUtility.RotateAroundPivot(angle, r.center);
        GUI.DrawTexture(r, tex);
        GUI.matrix = orig;
    }

    void OnDestroy()
    {
        if (_circleTex != null) Destroy(_circleTex);
        if (_starTex != null) Destroy(_starTex);
    }
}
