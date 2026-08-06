using UnityEngine;

public static class ChinesePlayerNameRandomizer
{
    private static readonly string[] Surnames =
    {
        "李", "王", "张", "刘", "陈", "杨", "赵", "黄", "周", "吴",
        "徐", "孙", "胡", "朱", "高", "林", "何", "郭", "马", "罗",
        "梁", "宋", "郑", "谢", "韩", "唐", "冯", "于", "董", "萧",
        "程", "曹", "袁", "邓", "许", "沈", "曾", "彭", "吕", "苏",
        "卢", "蒋", "蔡", "贾", "丁", "魏", "薛", "叶", "阎", "潘",
        "杜", "戴", "夏", "钟", "汪", "田", "任", "姜", "范", "方",
        "石", "姚", "谭", "廖", "邹", "熊", "金", "陆", "郝", "孔",
        "白", "崔", "康", "毛", "邱", "秦", "江", "史", "顾", "侯",
        "邵", "孟", "龙", "万", "段", "雷", "钱", "汤", "尹", "黎",
        "易", "常", "武", "乔", "贺", "赖", "龚", "文", "庞", "樊",
        "兰", "殷", "施", "陶", "洪", "翟", "安", "颜", "倪", "严"
    };

    private static readonly string[] GivenCharacters =
    {
        "明", "华", "芳", "伟", "秀", "英", "敏", "静", "丽", "强",
        "磊", "军", "洋", "勇", "艳", "杰", "娟", "涛", "超", "霞",
        "平", "刚", "桂", "辉", "玲", "红", "鹏", "飞", "雪", "梅",
        "兰", "竹", "菊", "松", "柏", "云", "雨", "风", "月", "星",
        "晨", "曦", "阳", "宁", "安", "乐", "康", "泰", "顺", "和",
        "德", "仁", "义", "礼", "智", "信", "忠", "孝", "廉", "耻",
        "文", "武", "斌", "博", "睿", "哲", "慧", "嘉", "佳", "怡",
        "悦", "欣", "涵", "萱", "彤", "瑶", "琪", "琳", "珊", "璐",
        "婷", "雯", "晴", "岚", "峰", "岳", "川", "海", "涛", "澜",
        "清", "澈", "润", "泽", "豪", "轩", "宇", "然", "辰", "逸",
        "航", "帆", "远", "志", "成", "达", "进", "升", "昌", "盛",
        "荣", "富", "贵", "福", "禄", "寿", "喜", "瑞", "祥", "吉"
    };

    public static string Generate()
    {
        bool useThreeCharacters = Random.value >= 0.5f;
        string surname = PickRandom(Surnames);

        if (!useThreeCharacters)
            return surname + PickRandom(GivenCharacters);

        string firstGiven = PickRandom(GivenCharacters);
        string secondGiven = PickRandom(GivenCharacters);

        while (secondGiven == firstGiven)
            secondGiven = PickRandom(GivenCharacters);

        return surname + firstGiven + secondGiven;
    }

    private static string PickRandom(string[] options)
    {
        return options[Random.Range(0, options.Length)];
    }
}
