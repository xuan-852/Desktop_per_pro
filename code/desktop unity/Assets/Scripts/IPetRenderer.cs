/// <summary>
/// 宠物渲染器接口 — 抽象 PNG 渲染和 Live2D 渲染的差异
/// </summary>
public interface IPetRenderer
{
    /// <summary>切换到拖拽姿势</summary>
    void ShowDragPose();

    /// <summary>点击区域枚举</summary>
    enum ClickZone { Head, Body, Feet, Unknown }

    /// <summary>切换到点击姿势（按区域不同反应）</summary>
    void ShowClickPose(ClickZone zone = ClickZone.Unknown);

    /// <summary>切换到落地姿势</summary>
    void ShowLandPose();

    /// <summary>切换到行走姿势</summary>
    void ShowWalkPose();

    /// <summary>切换到停止姿势，锁定 duration 秒不被覆盖</summary>
    void ShowStopPose(float lockSeconds);

    /// <summary>每帧更新渲染器（位置更新等）</summary>
    void OnPetUpdate(int petX, int petY, int petWidth, int petHeight,
                     int petVx, int petVy, bool onGround, bool isDragging, bool isPaused);
}
