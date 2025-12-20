namespace StationeersIC10Editor;

enum TokenType : int
{
    Namespace = 0,
    Type = 1,
    Class = 2,
    Enum = 3,
    Interface = 4,
    Struct = 5,
    TypeParameter = 6,
    Parameter = 7,
    Variable = 8,
    Property = 9,
    EnumMember = 10,
    Event = 11,
    Function = 12,
    Method = 13,
    Macro = 14,
    Keyword = 15,
    Modifier = 16,
    Comment = 17,
    String = 18,
    Number = 19,
    Regexp = 20,
    Operator = 21,
    Decorator = 22,
    Control = 23,
}

enum TokenTypeCompletion : int
{
    Unused = 0,
    Text = 1,
    Method = 2,
    Function = 3,
    Constructor = 4,
    Field = 5,
    Variable = 6,
    Class = 7,
    Interface = 8,
    Module = 9,
    Property = 10,
    Unit = 11,
    Value = 12,
    Enum = 13,
    Keyword = 14,
    Snippet = 15,
    Color = 16,
    File = 17,
    Reference = 18,
    Folder = 19,
    EnumMember = 20,
    Constant = 21,
    Struct = 22,
    Event = 23,
    Operator = 24,
    TypeParameter = 25,
}

class ColorTheme
{
    public uint[] Colors = new uint[]
    {
    /* 0 namespace */     ICodeFormatter.ColorFromHTML("#4EC9B0"),
    /* 1 type */          ICodeFormatter.ColorFromHTML("#4EC9B0"),
    /* 2 class */         ICodeFormatter.ColorFromHTML("#4EC9B0"),
    /* 3 enum */          ICodeFormatter.ColorFromHTML("#B8D7A3"),
    /* 4 interface */     ICodeFormatter.ColorFromHTML("#B8D7A3"),
    /* 5 struct */        ICodeFormatter.ColorFromHTML("#4EC9B0"),
    /* 6 typeParameter */ ICodeFormatter.ColorFromHTML("#fEC9B0"),
    /* 7 parameter */     ICodeFormatter.ColorFromHTML("#9CDCFE"),
    /* 8 variable */      ICodeFormatter.ColorFromHTML("#9CDCFE"),
    /* 9 property */      ICodeFormatter.ColorFromHTML("#9CDCFE"),
    /* 10 enumMember */    ICodeFormatter.ColorFromHTML("#B5CEA8"),
    /* 11 event */         ICodeFormatter.ColorFromHTML("#9CDCFE"),
    /* 12 function */      ICodeFormatter.ColorFromHTML("#DCDCAA"),
    /* 13 method */        ICodeFormatter.ColorFromHTML("#DCDCAA"),
    /* 14 macro */         ICodeFormatter.ColorFromHTML("#C586C0"),
    /* 15 keyword */       ICodeFormatter.ColorFromHTML("#569CD6"),
    /* 16 modifier */      ICodeFormatter.ColorFromHTML("#569CD6"),
    /* 17 comment */       ICodeFormatter.ColorFromHTML("#6A9955"),
    /* 18 string */        ICodeFormatter.ColorFromHTML("#D69D85"),
    /* 19 number */        ICodeFormatter.ColorFromHTML("#B5CEA8"),
    /* 20 regexp */        ICodeFormatter.ColorFromHTML("#D16969"),
    /* 21 operator */      ICodeFormatter.ColorFromHTML("#D4D4D4"),
    /* 22 decorator */     ICodeFormatter.ColorFromHTML("#C586C0"),
    /* 23 control */       ICodeFormatter.ColorFromHTML("#569CD6"),
    };

    public uint[] CompletionColors = new uint[]
    {
    /* 0 Unused */          ICodeFormatter.ColorFromHTML("#D4D4D4"),
    /* 1 Text */            ICodeFormatter.ColorFromHTML("#D4D4D4"),
    /* 2 Method */          ICodeFormatter.ColorFromHTML("#DCDCAA"),
    /* 3 Function */        ICodeFormatter.ColorFromHTML("#DCDCAA"),
    /* 4 Constructor */     ICodeFormatter.ColorFromHTML("#DCDCAA"),
    /* 5 Field */           ICodeFormatter.ColorFromHTML("#9CDCFE"),
    /* 6 Variable */        ICodeFormatter.ColorFromHTML("#9CDCFE"),
    /* 7 Class */           ICodeFormatter.ColorFromHTML("#4EC9B0"),
    /* 8 Interface */       ICodeFormatter.ColorFromHTML("#B8D7A3"),
    /* 9 Module */          ICodeFormatter.ColorFromHTML("#4EC9B0"),
    /* 10 Property */       ICodeFormatter.ColorFromHTML("#9CDCFE"),
    /* 11 Unit */           ICodeFormatter.ColorFromHTML("#B5CEA8"),
    /* 12 Value */          ICodeFormatter.ColorFromHTML("#B5CEA8"),
    /* 13 Enum */           ICodeFormatter.ColorFromHTML("#B8D7A3"),
    /* 14 Keyword */        ICodeFormatter.ColorFromHTML("#569CD6"),
    /* 15 Snippet */        ICodeFormatter.ColorFromHTML("#D4D4D4"),
    /* 16 Color */          ICodeFormatter.ColorFromHTML("#D16969"),
    /* 17 File */           ICodeFormatter.ColorFromHTML("#D4D4D4"),
    /* 18 Reference */      ICodeFormatter.ColorFromHTML("#D4D4D4"),
    /* 19 Folder */         ICodeFormatter.ColorFromHTML("#D4D4D4"),
    /* 20 EnumMember */     ICodeFormatter.ColorFromHTML("#B5CEA8"),
    /* 21 Constant */       ICodeFormatter.ColorFromHTML("#9CDCFE"),
    /* 22 Struct */         ICodeFormatter.ColorFromHTML("#4EC9B0"),
    /* 23 Event */          ICodeFormatter.ColorFromHTML("#9CDCFE"),
    /* 24 Operator */       ICodeFormatter.ColorFromHTML("#D4D4D4"),
    /* 25 TypeParameter */  ICodeFormatter.ColorFromHTML("#fEC9B0"),
    };

    public static ColorTheme Default = new ColorTheme();
}








