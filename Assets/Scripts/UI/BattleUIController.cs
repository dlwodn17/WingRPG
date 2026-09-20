using System;

namespace RPG25D.UI
{
    /// <summary>
    /// 기존 씬 및 에디터 툴과의 하위 호환성을 지원하는 프록시 클래스입니다.
    /// 신규 구현 및 데이터 바인딩은 BattleUIManager.cs를 통해 수행됩니다.
    /// </summary>
    [Obsolete("Use BattleUIManager instead for 3 vs 3 full battle loop.")]
    public class BattleUIController : BattleUIManager
    {
    }
}
