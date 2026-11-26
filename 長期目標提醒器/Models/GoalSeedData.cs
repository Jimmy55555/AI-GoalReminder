namespace 長期目標提醒器.Models;

public static class GoalSeedData
{
    public static readonly IReadOnlyList<Goal> Goals =
    [
        new Goal
        {
            Id = Guid.Parse("1a111111-1111-1111-1111-111111111111"),
            Title = "成為全端工程師",
            Description = "打好前端與後端基礎，能獨立完成一個完整產品。",
            TargetDate = new DateOnly(2027, 6, 30),
            Skills =
            [
                new Skill { Name = "C# / .NET", Status = SkillStatus.Mastered, Progress = 100, Note = "已完成兩個 API 專案" },
                new Skill { Name = "Blazor 元件", Status = SkillStatus.Learning, Progress = 65, Note = "熟悉元件參數與事件，正在練狀態管理" },
                new Skill { Name = "SQL 與資料建模", Status = SkillStatus.Learning, Progress = 40, Note = "可寫基本查詢，索引與效能調校待加強" },
                new Skill { Name = "Docker 部署", Status = SkillStatus.NotStarted, Progress = 0, Note = "下一步：完成容器化練習" }
            ]
        },
        new Goal
        {
            Id = Guid.Parse("2b222222-2222-2222-2222-222222222222"),
            Title = "取得日文 N1",
            Description = "以每天固定接觸的方式，累積到能閱讀原文技術文件。",
            TargetDate = new DateOnly(2027, 3, 31),
            Skills =
            [
                new Skill { Name = "N3 文法", Status = SkillStatus.Mastered, Progress = 100, Note = "已通过模擬考" },
                new Skill { Name = "N2 單字", Status = SkillStatus.Learning, Progress = 72, Note = "每日 20 字，持續複習" },
                new Skill { Name = "聽力", Status = SkillStatus.Learning, Progress = 45, Note = "Podcast 每天 15 分鐘" },
                new Skill { Name = "N1 閱讀", Status = SkillStatus.NotStarted, Progress = 0, Note = "預計明年一月開始" }
            ]
        },
        new Goal
        {
            Id = Guid.Parse("3c333333-3333-3333-3333-333333333333"),
            Title = "完成半程馬拉松",
            Description = "建立穩定跑量，能在 2 小時內完成 21 公里。",
            TargetDate = new DateOnly(2027, 9, 30),
            Skills =
            [
                new Skill { Name = "基礎耐力", Status = SkillStatus.Mastered, Progress = 100, Note = "每週可穩定跑 20 公里" },
                new Skill { Name = "配速策略", Status = SkillStatus.Learning, Progress = 55, Note = "練習負分段配速" },
                new Skill { Name = "核心肌力", Status = SkillStatus.Learning, Progress = 30, Note = "每週兩次重訓" },
                new Skill { Name = "賽事補給", Status = SkillStatus.NotStarted, Progress = 0, Note = "待長跑時測試" }
            ]
        }
    ];
}
