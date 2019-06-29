using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Collections.ObjectModel;

namespace PL0_Compiler
{
    /// <summary>
    /// MainWindow.xaml 的交互逻辑
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
            this.SymbolList.Clear();
            this.SymbolTableView.ItemsSource = SymbolList;
            //Interpret_Btn.Visibility = Visibility.Hidden;
            Interpret_Btn.IsEnabled = false;
        }

        //关于词法分析
        //保留字
        string[] reserved = { "begin", "end", "if", "then", "else", "const", "procedure", "var", "odd", "while", "do", "call", "read", "write", "repeat", "until" };

        enum SYMBOL
        {
            NONE, NUMBER, KEYWORD, IDENTIFIER, PLUS, SUBTRACT, MULTIPLY, DIVIDE,
            NOTEQUAL, LESS, LESSANDEQUAL, GREATER, GREATERANDEQUAL, EQUAL, ASSIGNMENT, CONSTASSIGN,
            COMMA, PERIOD, COLON, SEMICOLON, LEFTPARENTHESIS, RIGHTPARENTHESIS
        };//共21  SEMICOLON为分号
        /*+ - * / 
         * <> < <= > >= = := = 
         * , . ; ( ) 17*/
        SYMBOL symbol = 0;
        string sen;//当前行
        char CHAR;//当前字符
        string TOKEN;//当前单词
        bool isConst = false;//判断是否为常量，以此来判断CONSTASSIGN
        int ptr;//当前字符位置
        int len;//每行长度
        int line;//当前行数
        int errornum;
        List<string> txt = new List<string>();//存放文本文档的每一行

        //关于语法分析
        int nowline = 0;//语法分析当前行
        int nowword = 0;//语法分析当前单词在当前行的位置
        int nowsymnum = 0;//当前读取的词汇From SymbolList
        string nowsymname;
        string nowsymtype;
        string nowsymval;

        List<string> declbeginsym = new List<string>();//声明的FIRST集
        List<string> statebeginsym = new List<string>();//语句的FIRST集
        List<string> facbeginsym = new List<string>();//因子的FIRST集

        //int symtabindex = 0; //符号表尾指针 是局部变量 相对block开始计数
        int codeindex = 0; //PCode Index
        const int MAXLEVEL = 3;
        //int dx = 0;//data allocation index,每层block重新计数

        //Interpret
        string[] input;
        int inputtop=0;

        //ErrorList
        List<string> ErrorList = new List<string>();
        int gerrorcnt = 0;//语法错误
        const int MAXERROR = 30;//当前所报最大错误数，词法错误和语法错误各自计数

        //符号表
        class symboltab
        {
            string _name;
            string _kind;
            int _val;
            int _level;
            int _addr;
            int _size;

            public string Name
            {
                get { return _name; }
                set { _name = value; }
            }
            public string Kind//constant variable procedure
            {
                get { return _kind; }
                set { _kind = value; }
            }
            public int Val
            {
                get { return _val; }
                set { _val = value; }
            }
            public int Level
            {
                get { return _level; }
                set { _level = value; }
            }
            public int Addr
            {
                get { return _addr; }
                set { _addr = value; }
            }
            public int Size
            {
                get { return _size; }
                set { _size = value; }
            }
            public symboltab(string name, string kind, int val, int level, int addr, int size)
            {
                _name = name;
                _kind = kind;
                _val = val;
                _level = level;
                _addr = addr;
                _size = size;
            }
            ~symboltab()
            {
            }
        }
        List<symboltab> SymbolTab = new List<symboltab>();

        //PCode指令集
        string[] PCode = { "LIT", "OPR", "LOD", "STO", "CAL", "INT", "JMP", "JPC", "RED", "WRT" };

        class SymbolTable
        {
            private string _token;
            private string _symbol;
            private string _value;
            private int _line;

            public string Token
            {
                get { return _token; }
                set { _token = value; }
            }
            public string Symbol
            {
                get { return _symbol; }
                set { _symbol = value; }
            }
            public string Value
            {
                get { return _value; }
                set { _value = value; }
            }
            public int Line
            {
                get { return _line; }
                set { _line = value; }
            }
            public SymbolTable(string token, string symbol, string value, int line)
            {
                this._token = token;
                this._symbol = symbol;
                this._value = value;
                this._line = line;
            }
            ~SymbolTable()
            {
            }
        }

        class PCodeTable
        {
            private int _number;
            private string _operation;
            private int _layer;
            private int _address;

            public int Number
            {
                get { return _number; }
                set { _number = value; }
            }

            public string Operation
            {
                get { return _operation; }
                set { _operation = value; }
            }
            public int Layer
            {
                get { return _layer; }
                set { _layer = value; }
            }
            public int Address
            {
                get { return _address; }
                set { _address = value; }
            }
            public PCodeTable(int number, string operation, int layer, int address)
            {
                this._number = number;
                this._operation = operation;
                this._layer = layer;
                this._address = address;
            }
            ~PCodeTable()
            {
            }
        }

        //SymbolList存储词法分析的结果，作为SymbolTableView的数据源
        ObservableCollection<SymbolTable> SymbolList = new ObservableCollection<SymbolTable>();
        //PCodeList存储PCode指令，作为PCode_ListView的数据源
        ObservableCollection<PCodeTable> PCodeList = new ObservableCollection<PCodeTable>();

        //词法分析From Here
        private void addToList(string tok, string sym, string val, int line)
        {
            SymbolList.Add(new SymbolTable(tok, sym, val, line));
            this.SymbolTableView.ItemsSource = SymbolList;
        }

        private void error(int x, int y, string errortype)
        {
            errornum++;
            string tmp = "Lexical Analysis Error " + errornum.ToString() + ": (Line " + x.ToString() + " Column " + y.ToString() + ") " + errortype + "\n";
            if (errornum <= MAXERROR)
                this.ErrorText.Text += tmp;
            readChar();
        }

        private bool isDigit(char c)
        {
            if (c <= '9' && c >= '0')
                return true;
            else
                return false;
        }

        private bool isLetter(char c)
        {
            if (c >= 'A' && c <= 'Z')
                return true;
            if (c >= 'a' && c <= 'z')
                return true;
            return false;
        }

        private string numToBin(string num)
        {
            string bin = "";
            int i = 0;
            for (i = 0; i < num.Length; i++)
            {
                if (num[i] == '.')
                    break;
            }
            if (i == num.Length)
            {
                bin = "";
                int n = Convert.ToInt32(num);
                bin = Convert.ToString(n, 2);
                /*if (n == 0)
                    return "0";
                while (n != 0)
                {
                    string tmp = "0";
                    if (n % 2 == 1)
                        tmp = "1";
                    //char tmp = (char)('0' + (n % 2));
                    //MessageBox.Show(tmp.ToString());
                    bin = bin.Insert(0, tmp);
                    n = n >> 1;
                }*/
            }
            else
            {
                bin = "";
                string front = num.Substring(0, i);
                int n = Convert.ToInt32(front);
                if (n == 0)
                    bin += "0.";
                bin = Convert.ToString(n, 2);
                bin += '.';
                int cnt = 30;
                string back = "0." + num.Substring(i + 1, num.Length - i - 1);
                double m = Convert.ToDouble(back);
                //MessageBox.Show(m.ToString());
                while (m != 0.0 && cnt > 0)
                {
                    m *= 2;
                    bin += (m >= 1.0) ? '1' : '0';
                    m -= (int)m;
                    //MessageBox.Show(m.ToString());
                    cnt--;
                }
            }
            //MessageBox.Show(bin);
            return bin;
        }

        private void CAT()
        {
            if (TOKEN == "")
                TOKEN += CHAR;
            else
            {
                if (symbol == SYMBOL.NUMBER)
                {
                    if (!isDigit(CHAR))
                    {
                        if (CHAR == '.' && ptr != len - 1)
                        {
                            TOKEN += CHAR;
                        }
                        else
                        {
                            addToList(TOKEN, symbol.ToString(), numToBin(TOKEN), line);
                            symbol = SYMBOL.NONE;
                            TOKEN = "";
                            ptr--;
                            //if(isLetter(CHAR))
                            //{
                            //    error(line, ptr+1, "Lack of Operator!");
                            //}
                        }
                    }
                    else
                        TOKEN += CHAR;
                }
                else if (symbol == SYMBOL.IDENTIFIER)
                {
                    if (!isDigit(CHAR) && !isLetter(CHAR))
                    {
                        if (reserved.Contains(TOKEN))
                        {
                            symbol = SYMBOL.KEYWORD;
                            if (TOKEN == "const")
                                isConst = true;
                        }
                        addToList(TOKEN, symbol.ToString(), TOKEN, line);
                        symbol = SYMBOL.NONE;
                        TOKEN = "";
                        ptr--;
                    }
                    else
                        TOKEN += CHAR;
                }
                else if (symbol == SYMBOL.NOTEQUAL || symbol == SYMBOL.LESSANDEQUAL || symbol == SYMBOL.GREATERANDEQUAL || symbol == SYMBOL.ASSIGNMENT)
                {
                    TOKEN += CHAR;
                    addToList(TOKEN, symbol.ToString(), TOKEN, line);
                    symbol = SYMBOL.NONE;
                    TOKEN = "";
                }
                else if (symbol != SYMBOL.NONE)
                {
                    addToList(TOKEN, symbol.ToString(), TOKEN, line);
                    symbol = SYMBOL.NONE;
                    TOKEN = "";
                    ptr--;
                }
            }
            //ptr++;
            readChar();
        }

        private void readChar()
        {
            while (ptr < len && symbol == SYMBOL.NONE && (sen[ptr] == ' ' || sen[ptr] == '\t' || sen[ptr] == '\n'))
            {
                ptr++;
            }
            if (ptr < len)
            {
                CHAR = sen[ptr++];

                if (symbol == SYMBOL.NONE)
                {
                    if (isDigit(CHAR))
                    {
                        symbol = SYMBOL.NUMBER;//1
                    }
                    else if (isLetter(CHAR))
                    {
                        symbol = SYMBOL.IDENTIFIER;//3
                    }
                    else if (CHAR == '+')
                    {
                        symbol = SYMBOL.PLUS;//4
                    }
                    else if (CHAR == '-')
                    {
                        symbol = SYMBOL.SUBTRACT;//5
                    }
                    else if (CHAR == '*')
                    {
                        symbol = SYMBOL.MULTIPLY;//6
                    }
                    else if (CHAR == '/')
                    {
                        symbol = SYMBOL.DIVIDE;//7
                    }
                    else if (CHAR == '<')
                    {
                        symbol = SYMBOL.LESS;//9
                    }
                    else if (CHAR == '>')
                    {
                        symbol = SYMBOL.GREATER;//11
                    }
                    else if (CHAR == ':')
                    {
                        symbol = SYMBOL.COLON;//18
                    }
                    else if (CHAR == '=')
                    {
                        if (isConst)
                        {
                            symbol = SYMBOL.CONSTASSIGN;//15
                        }
                        else
                            symbol = SYMBOL.EQUAL;//13
                    }
                    else if (CHAR == ',')
                    {
                        symbol = SYMBOL.COMMA;//16
                    }
                    else if (CHAR == '.')
                    {
                        symbol = SYMBOL.PERIOD;//17
                    }
                    else if (CHAR == ';')
                    {
                        isConst = false;
                        symbol = SYMBOL.SEMICOLON;//19
                    }
                    else if (CHAR == '(')
                    {
                        symbol = SYMBOL.LEFTPARENTHESIS;//21
                    }
                    else if (CHAR == ')')
                    {
                        symbol = SYMBOL.RIGHTPARENTHESIS;//22
                    }
                    else if (CHAR == '#' && ptr == len)
                    {

                    }
                    else
                    {
                        symbol = SYMBOL.NONE;
                        TOKEN = "";
                        error(line, ptr, "未能识别的符号");
                    }
                }
                else
                {
                    if (symbol == SYMBOL.COLON)
                    {
                        if (CHAR == '=')
                            symbol = SYMBOL.ASSIGNMENT;//14
                        else
                        {
                            symbol = SYMBOL.NONE;
                            TOKEN = "";
                            //CHAR = '\0';
                            /*error(line, --ptr, "Wrong Operator!");之前是用来词法分析的const后的:=的纠错，现在不需要*/
                        }
                    }
                    if (symbol == SYMBOL.LESS)
                    {
                        if (CHAR == '>')
                            symbol = SYMBOL.NOTEQUAL;//8
                        else if (CHAR == '=')
                            symbol = SYMBOL.LESSANDEQUAL;//10
                    }
                    if (symbol == SYMBOL.GREATER)
                    {
                        if (CHAR == '=')
                            symbol = SYMBOL.GREATERANDEQUAL;//12
                    }
                }
                CAT();
            }
        }

        private void startAnalysis(string sentence)
        {
            CHAR = '\0';
            TOKEN = "";
            ptr = 0;
            sen = sentence;
            len = sentence.Length;
            readChar();
        }

        //语法分析From Here
        private void startGrammaAnalysis()
        {
            //ErrorList 初始化
            gerrorcnt = 0;
            ErrorList.Add("");//0
            ErrorList.AddRange(new string[] {
                "应为 = 而不是 :=",/*1*/
                "= 后应为数",
                "标识符后应为 =",
                "const,var,procedure后应为标识符",
                "漏掉逗号或分号",/*5*/
                "过程说明后的符号不正确",
                "应为语句开始符号",
                "程序体内语句部分后的符号不正确",
                "应为句号",
                "语句之间漏分号",/*10*/
                "标识符未声明",
                "不可向常量或过程赋值",
                "应为赋值运算符 :=",
                "call后应为标识符",
                "不可调用常量或变量"/*15*/,
                "if语句缺少then",
                "应为分号或end",
                "while语句缺少do",
                "语句后的符号不正确",
                "应为关系运算符",/*20*/
                "表达式内不可有过程标识符",
                "缺少右括号",
                "因子后不可为此符号",
                "表达式不能以此符号开始",
                "标识符已声明",/*25*/
                "嵌套层数过多，大于3",
                "read语句括号内不是标识符",
                "repeat语句缺少until",
                "句号后还有多余部分"});
            ErrorList.AddRange(new string[20]);
            ErrorList[30] = "这个数太大";
            ErrorList[40] = "应为右括号";

            nowline = 0;
            nowword = 0;
            nowsymnum = 0;
            nowsymname = nowsymtype = nowsymval = "";
            codeindex = 0;
            PCodeList.Clear();
            PCode_ListView.ItemsSource = PCodeList;
            SymbolTab.Clear();
            Interpret_Btn.IsEnabled = false;

            declbeginsym.Clear();
            declbeginsym.Add("const");
            declbeginsym.Add("var");
            declbeginsym.Add("procedure");

            statebeginsym.Clear();
            statebeginsym.Add("begin");
            statebeginsym.Add("call");
            statebeginsym.Add("if");
            statebeginsym.Add("while");
            statebeginsym.Add("repeat");
            statebeginsym.Add("read");
            statebeginsym.Add("write");


            facbeginsym.Clear();
            facbeginsym.Add(SYMBOL.IDENTIFIER.ToString());
            facbeginsym.Add(SYMBOL.NUMBER.ToString());
            facbeginsym.Add(SYMBOL.LEFTPARENTHESIS.ToString());

            int tableindex = 0;
            getsym();
            var x = declbeginsym.Union(statebeginsym).ToArray();
            List<string> tmp = x.ToList();
            tmp.Add(SYMBOL.PERIOD.ToString());
            block(tableindex, 0, tmp);

            if (nowsymtype != SYMBOL.PERIOD.ToString())//判断最后的符号是否为句号
            {
                //error lack period
                grammarError(9);
            }
            else if(getsym())
            {
                grammarError(29);
            }

            if (errornum == 0 && gerrorcnt == 0)//编译成功
            {
                ErrorText.Text = "编译成功！";
                //Interpret_Btn.Visibility = Visibility.Visible;
                Interpret_Btn.IsEnabled = true;
            }
            /*print error*/
            //PCode_ListView.ItemsSource = PCodeList;
        }

        private void grammarError(int errornum)
        {
            gerrorcnt++;
            string tmp = "Error ";
            tmp += gerrorcnt.ToString() + ": ( Line " + nowline.ToString() + "," + nowword.ToString() + " )";
            tmp += ErrorList[errornum] + "\n";
            if (gerrorcnt <= MAXERROR)
                ErrorText.Text += tmp;
        }

        private void showPCode()
        {
            PCode_ListView.ItemsSource = PCodeList;
        }

        private bool getsym()//从词汇表中取出单词
        {
            if (nowsymnum < SymbolList.Count)
            {
                if (SymbolList[nowsymnum].Line != nowline)
                {
                    nowword = 0;
                    nowline = SymbolList[nowsymnum].Line;
                }
                nowsymname = SymbolList[nowsymnum].Token;
                nowsymtype = SymbolList[nowsymnum].Symbol;
                nowsymval = SymbolList[nowsymnum].Value;
                nowword++;
                nowsymnum++;
                return true;
            }
            return false;
        }

        private bool test(List<string> list1, List<string> list2, int n)//n is wrong code
        {
            /*检测后继符号合法性
             * list1为当前语法成分合法的后继符号
             * list2为停止符号集合*/
            if (nowsymnum > SymbolList.Count)
                return false;
            if (!list1.Contains(nowsymname) && !list1.Contains(nowsymtype))
            {
                //error number n error
                grammarError(n);
                //if (nowsymnum < SymbolList.Count)
                while (!list1.Contains(nowsymname) && !list1.Contains(nowsymtype) && !list2.Contains(nowsymname) && !list2.Contains(nowsymtype))
                {
                    if (!getsym())
                        return false;
                }
            }
            return true;
        }

        private bool gen(string ope, int lev, int add)//保存PCode到PCodeList
        {
            //还有Pcode超范围的处理代码，但是此程序目前默认无上限
            PCodeList.Add(new PCodeTable(codeindex, ope, lev, add));
            codeindex++;
            PCode_ListView.ItemsSource = PCodeList;
            return true;
        }

        private int position(int symtabindex, string name)
        {
            //倒序查找的目的是先查找当前层的标识符，未声明则到上层查找，直到遍历符号栈
            int i = 0;
            for (i = symtabindex; i > 0; i--)
            {
                if (SymbolTab[i].Name == name)
                    return i;
            }
            return i;
        }

        private void enter(string name, string kind, int val, int level, ref int symtabindex, ref int dx)//登录符号表
        {
            //因为是判断是否声明，所以只在当前层查找，要有level和name两个参数进行匹配
            for (int i = symtabindex; i > 0; i--)
            {
                if (SymbolTab[i].Name == name && SymbolTab[i].Level == level)
                {
                    //error identifier repeat
                    grammarError(25);
                    break;
                }
            }
            symtabindex++;//start from 1

            /*下句if也可以*/
            while (symtabindex >= SymbolTab.Count)//如果超出范围 拓展符号表
            {
                SymbolTab.Add(new symboltab("", "", 0, 0, 0, 0));
            }

            SymbolTab[symtabindex].Name = name;
            SymbolTab[symtabindex].Kind = kind;
            switch (kind)
            {
                case "constant":
                    SymbolTab[symtabindex].Val = val;
                    break;
                case "variable":
                    SymbolTab[symtabindex].Addr = dx++;
                    SymbolTab[symtabindex].Level = level;
                    break;
                case "procedure":
                    SymbolTab[symtabindex].Level = level;
                    break;
            }

        }

        private bool block(int symtabindex, int level, List<string> firstsys)//分程序
        {
            int cx0, tx0 = 0;
            tx0 = symtabindex;//记录当前的符号栈指针
            while (tx0 >= SymbolTab.Count)//当前符号栈指针超出范围，增加符号表
            {
                SymbolTab.Add(new symboltab("", "", 0, 0, 0, 0));
            }
            SymbolTab[tx0].Addr = codeindex;
            int dx = 3;//data allocation index 三个空间存放：静态链SL、动态链DL和返回地址RA

            if (!gen("JMP", 0, 0))//JMP from declaration part to statement part
                return false;

            if (level > MAXLEVEL)//level超出范围
            {
                //error level is too much
                grammarError(26);
                return false;
            }

            do//书上如此，但是感觉不遵循原文法
            {
                if (nowsymval == "const")//判断为const，进行常量声明的处理
                {
                    if (!getsym())
                        return false;
                    do//书上如此，目的是为了处理缺失逗号
                    {
                        constdeclaration(ref symtabindex, ref dx, level);//跳转到常量处理
                        while (nowsymtype == SYMBOL.COMMA.ToString())//判断为逗号，继续常量声明处理
                        {
                            if (!getsym())
                                return false;
                            constdeclaration(ref symtabindex, ref dx, level);//跳转到常量处理
                        }
                        if (nowsymtype == SYMBOL.SEMICOLON.ToString())//判断为分号，结束常量声明的处理
                        {
                            if (!getsym())
                                return false;
                            break;
                        }
                        else
                        {
                            //error here should be semicolon
                            grammarError(5);
                        }
                    } while (nowsymtype == SYMBOL.IDENTIFIER.ToString());
                }
                if (nowsymval == "var")
                {
                    if (!getsym())
                        return false;
                    do//同上
                    {
                        vardeclaration(level, ref symtabindex, ref dx);//进行变量声明的处理
                        while (nowsymtype == SYMBOL.COMMA.ToString())//判断为逗号，循环进行变量声明的处理
                        {
                            if (!getsym())
                                return false;
                            vardeclaration(level, ref symtabindex, ref dx);
                        }
                        if (nowsymtype == SYMBOL.SEMICOLON.ToString())//判断为分号，终止变量声明的处理
                        {
                            if (!getsym())
                                return false;
                            break;
                        }
                        else
                        {
                            //error here should be semicolon
                            grammarError(5);
                        }
                    } while (nowsymtype == SYMBOL.IDENTIFIER.ToString());
                }
                while (nowsymval == "procedure")//判断为过程
                {
                    if (!getsym())
                        return false;
                    if (nowsymtype == SYMBOL.IDENTIFIER.ToString())//是标识符
                    {
                        enter(nowsymname, "procedure", 0, level, ref symtabindex, ref dx);//登录符号表添加符号
                        if (!getsym())
                            return false;
                    }
                    else
                    {
                        //error here should be identifier
                        grammarError(4);
                    }
                    if (nowsymtype == SYMBOL.SEMICOLON.ToString())//结束当前声明
                    {
                        if (!getsym())
                            return false;
                    }
                    else
                    {
                        //error here should be semicolon
                        grammarError(5);
                    }
                    var x = firstsys.ToArray();
                    List<string> tmp = x.ToList();
                    tmp.Add(SYMBOL.SEMICOLON.ToString());
                    if (!block(symtabindex, level + 1, tmp))//进行下一层分程序的处理
                        return false;
                    if (nowsymtype == SYMBOL.SEMICOLON.ToString())//procedure结束
                    {
                        if (!getsym())//now sym is prepare for statement
                            return false;
                        var x2 = statebeginsym.ToArray();
                        List<string> tmp2 = x2.ToList();
                        tmp2.Add(SYMBOL.IDENTIFIER.ToString());
                        tmp2.Add(SYMBOL.PERIOD.ToString());//Last change
                        tmp2.Add("procedure");
                        if (!test(tmp2, firstsys, 6))//测试下一个符号是否为过程之后的符号
                            return false;
                    }
                    else
                    {
                        //error here should be semicolon
                        grammarError(5);
                    }
                }
                var x3 = statebeginsym.ToArray();
                List<string> tmp3 = x3.ToList();
                tmp3.Add(SYMBOL.IDENTIFIER.ToString());
                tmp3.Add(SYMBOL.PERIOD.ToString());//Last change
                if (!test(tmp3, declbeginsym, 7))//测试下一个符号是否为语句的开始符号
                    return false;
            } while (declbeginsym.Contains(nowsymtype) || declbeginsym.Contains(nowsymname));//nowsym in declbeginsym eg:PLUS is nowsymtype, begin is nowsymname

            //修改符号表和PCode中的地址//回填地址
            PCodeList[SymbolTab[tx0].Addr].Address = codeindex;
            SymbolTab[tx0].Addr = codeindex;
            cx0 = codeindex;
            gen("INT", 0, dx);

            var x4 = firstsys.ToArray();
            List<string> tmp4 = x4.ToList();
            tmp4.Add(SYMBOL.SEMICOLON.ToString());
            tmp4.Add(SYMBOL.PERIOD.ToString());//Last change
            tmp4.Add("end");
            statement(ref symtabindex, level, tmp4);//开始语句分析
            gen("OPR", 0, 0);

            List<string> tmp5 = new List<string>();
            if (!test(firstsys, tmp5, 8)) //判断是否为程序体内语句部分后的符号
                return false;
            //showPCode();
            return true;
        }

        private bool constdeclaration(ref int symtabindex, ref int dx, int level)//常量定义处理
        {
            if (nowsymtype == SYMBOL.IDENTIFIER.ToString())
            {
                string constantname = nowsymname;//暂时存储当前的常量名
                if (!getsym())
                    return false;
                if (nowsymtype == SYMBOL.ASSIGNMENT.ToString() || nowsymtype == SYMBOL.CONSTASSIGN.ToString())//判断为:=或=，表示是赋值
                {
                    if (nowsymtype == SYMBOL.ASSIGNMENT.ToString())
                    {
                        //error here is = but not :=
                        grammarError(1);
                    }
                    if (!getsym())
                        return false;
                    if (nowsymtype == SYMBOL.NUMBER.ToString())//判断为数字，登录符号表记录该常量
                    {
                        enter(constantname, "constant", Convert.ToInt32(nowsymname), level, ref symtabindex, ref dx);
                        if (!getsym())
                            return false;
                    }
                    else
                    {
                        //error here should be number
                        grammarError(2);
                    }

                }
                else
                {
                    //error here should be =
                    grammarError(3);
                }
            }
            else
            {
                //error here should be identifier
                grammarError(4);
            }
            return true;
        }

        private bool vardeclaration(int level, ref int symtabindex, ref int dx)//变量说明处理
        {
            if (nowsymtype == SYMBOL.IDENTIFIER.ToString())//登录符号表记录变量，暂时不记录地址，之后回填
            {
                enter(nowsymname, "variable", 0, level, ref symtabindex, ref dx);
                if (!getsym())
                    return false;
            }
            else
            {
                //error here should be identifier
                grammarError(4);
            }
            return true;
        }

        private bool statement(ref int symtabindex, int level, List<string> firstsys)//语句部分
        {
            int i, cx1, cx2;
            if (nowsymtype == SYMBOL.IDENTIFIER.ToString())//进行赋值语句的分析
            {
                i = position(symtabindex, nowsymname);
                if (i == 0)//标识符未声明
                {
                    //error no such identifier
                    grammarError(11);
                }
                else
                {
                    if (SymbolTab[i].Kind != "variable")//判断当前标识符是否为变量
                    {
                        //error this is not a variable
                        grammarError(12);
                        i = 0;
                    }
                    if (!getsym())
                        return false;
                    if (nowsymtype == SYMBOL.ASSIGNMENT.ToString())
                    {
                        if (!getsym())
                            return false;
                    }
                    else if (nowsymtype == SYMBOL.EQUAL.ToString() || nowsymtype == SYMBOL.CONSTASSIGN.ToString())
                    {
                        //error here should be assignment
                        grammarError(13);
                    }
                    expression(ref symtabindex, level, firstsys);//赋值后的表达式
                    if (i != 0)
                        gen("STO", level - SymbolTab[i].Level, SymbolTab[i].Addr);
                }
            }
            else if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "if")//进行条件语句的分析
            {
                if (!getsym())
                    return false;
                var x = firstsys.ToArray();
                List<string> tmp = x.ToList();
                tmp.Add("then");
                tmp.Add("do");
                condition(ref symtabindex, level, tmp);//if后的条件
                if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "then")
                {
                    if (!getsym())
                        return false;
                }
                else
                {
                    //error here should be then
                    grammarError(16);
                }
                cx1 = codeindex;//记录当前的PCode位置
                gen("JPC", 0, 0);
                var x2 = firstsys.ToArray();
                List<string> tmp2 = x2.ToList();
                tmp2.Add("else");
                statement(ref symtabindex, level, tmp2);//then后的语句分析

                if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "else")
                {
                    if (!getsym())
                        return false;
                    cx2 = codeindex;//记录当前的PCode位置
                    gen("JMP", 0, 0);
                    PCodeList[cx1].Address = codeindex;//回填地址
                    statement(ref symtabindex, level, firstsys);
                    PCodeList[cx2].Address = codeindex;//回填地址
                }
                else
                {
                    PCodeList[cx1].Address = codeindex;//回填地址
                }
            }
            else if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "while")//进行当型循环语句的分析
            {
                cx1 = codeindex;//记录当前PCode位置
                if (!getsym())
                    return false;
                var x = firstsys.ToArray();
                List<string> tmp = x.ToList();
                tmp.Add("do");
                condition(ref symtabindex, level, tmp);//递归下降到条件
                cx2 = codeindex;//记录当前PCode位置
                gen("JPC", 0, 0);
                if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "do")
                {
                    if (!getsym())
                        return false;
                }
                else
                {
                    //error here should be do
                    grammarError(18);
                }
                statement(ref symtabindex, level, firstsys);//do后的语句分析
                gen("JMP", 0, cx1);
                PCodeList[cx2].Address = codeindex;//回填地址
            }
            else if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "call")//进行过程调用语句的分析
            {
                i = 0;
                if (!getsym())
                    return false;
                if (nowsymtype != SYMBOL.IDENTIFIER.ToString())//call后应为类型为procedure的标识符
                {
                    //error should be identifier
                    grammarError(14);
                }
                else
                {
                    i = position(symtabindex, nowsymname);
                    if (i == 0)
                    {
                        //error no such identifier
                        grammarError(11);
                    }
                    else
                    {
                        if (SymbolTab[i].Kind == "procedure")
                        {
                            gen("CAL", level - SymbolTab[i].Level, SymbolTab[i].Addr);
                        }
                        else
                        {
                            //error should be procedure
                            grammarError(15);
                        }
                        if (!getsym())
                            return false;
                    }
                }
            }
            else if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "read")//进行读语句的分析
            {
                i = 0;
                if (!getsym())
                    return false;
                if (nowsymtype == SYMBOL.LEFTPARENTHESIS.ToString())
                {
                    do
                    {
                        if (!getsym())
                            return false;
                        if (nowsymtype == SYMBOL.IDENTIFIER.ToString())
                        {
                            i = position(symtabindex, nowsymname);
                            if (i == 0)
                            {
                                //error no such identifier
                                grammarError(11);
                            }
                            else
                            {
                                if (SymbolTab[i].Kind != "variable")
                                {
                                    //error should be variable
                                    grammarError(12);
                                    i = 0;
                                }
                                else
                                {
                                    gen("RED", level - SymbolTab[i].Level, SymbolTab[i].Addr);
                                }
                                //getsym();
                            }
                        }
                        else
                        {
                            //error should be identifier
                            grammarError(27);
                        }
                        if (!getsym())
                            return false;
                    } while (nowsymtype == SYMBOL.COMMA.ToString());//多个符号，以COMMA分隔，循环进行分析
                }
                else
                {
                    //error 40 should be leftparenthesis
                    grammarError(40);
                }
                if (nowsymtype != SYMBOL.RIGHTPARENTHESIS.ToString())
                {
                    //error shoule be rightparenthesis
                    grammarError(22);
                }
                if (!getsym())
                    return false;
            }
            else if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "write")//进行写语句的分析
            {
                i = 0;
                if (!getsym())
                    return false;
                if (nowsymtype == SYMBOL.LEFTPARENTHESIS.ToString())
                {
                    var x = firstsys.ToArray();
                    List<string> tmp = x.ToList();
                    tmp.Add(SYMBOL.RIGHTPARENTHESIS.ToString());
                    tmp.Add(SYMBOL.COMMA.ToString());
                    do
                    {
                        if (!getsym())
                            return false;
                        expression(ref symtabindex, level, tmp);//读语句中的表达式
                        gen("WRT", 0, 0);
                    } while (nowsymtype == SYMBOL.COMMA.ToString());//写语句有多个表达式，以COMMA分隔，循环进行分析
                    if (nowsymtype != SYMBOL.RIGHTPARENTHESIS.ToString())
                    {
                        //error shoule be rightparenthesis
                        grammarError(22);
                    }
                    if (!getsym())
                        return false;
                }
                else
                {
                    //error 40 should be leftparenthesis
                    grammarError(40);
                }
            }
            else if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "begin")//进行复合语句的分析
            {
                if (!getsym())
                    return false;
                var x = firstsys.ToArray();
                List<string> tmp = x.ToList();
                tmp.Add(SYMBOL.SEMICOLON.ToString());
                tmp.Add("end");
                statement(ref symtabindex, level, tmp);//复合语句中的语句分析

                var x2 = statebeginsym.ToArray();
                List<string> tmp2 = x2.ToList();
                tmp2.Add(SYMBOL.SEMICOLON.ToString());
                while (tmp2.Contains(nowsymtype) || tmp2.Contains(nowsymname))//多个语句的话，循环分析
                {
                    if (nowsymtype == SYMBOL.SEMICOLON.ToString())
                    {
                        if (!getsym())
                            return false;
                    }
                    else
                    {
                        //error 10 lack semicolon
                        grammarError(10);
                    }
                    statement(ref symtabindex, level, tmp);
                }
                if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "end")
                {
                    if (!getsym())
                        return false;
                }
                else
                {
                    //error 17 should be end
                    grammarError(17);
                }
            }
            else if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "repeat")//进行重复语句的分析
            {
                cx1 = codeindex;//记录当前PCode的位置
                if (!getsym())
                    return false;
                var x = firstsys.ToArray();
                List<string> tmp = x.ToList();
                tmp.Add(SYMBOL.SEMICOLON.ToString());
                tmp.Add("until");
                statement(ref symtabindex, level, tmp);//重复语句中的语句分析

                /*书上如此，但是实际应该插入statement的FIRST集和分号
                 * List<string> tmp2 = new List<string>();
                tmp2.Add("begin");
                tmp2.Add("call");
                tmp2.Add("if");
                tmp2.Add("while");*/
                var x2 = statebeginsym.ToArray();
                List<string> tmp2 = x2.ToList();
                tmp2.Add(SYMBOL.SEMICOLON.ToString());
                while (tmp2.Contains(nowsymtype) || tmp2.Contains(nowsymname))//因为repeat中可以有多个语句，以SEMICOLON分隔，循环分析
                {
                    if (nowsymtype == SYMBOL.SEMICOLON.ToString())
                    {
                        if (!getsym())
                            return false;
                    }
                    else
                    {
                        //error 10 lack semicolon
                        grammarError(10);
                    }
                    var x3 = firstsys.ToArray();
                    List<string> tmp3 = x3.ToList();
                    tmp3.Add(SYMBOL.SEMICOLON.ToString());
                    tmp3.Add("until");
                    statement(ref symtabindex, level, tmp3);
                }
                if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "until")
                {
                    if (!getsym())
                        return false;
                    condition(ref symtabindex, level, firstsys);//until之后的条件分析
                    gen("JPC", 0, cx1);
                }
                else
                {
                    //error should be until
                    grammarError(28);
                }
            }
            List<string> tmp4 = new List<string>();
            if (!test(firstsys, tmp4, 19))//测试是否为语句后的符号
                return false;
            return true;
        }

        private bool expression(ref int symtabindex, int level, List<string> firstsys)//表达式
        {
            string symtmp;
            var x = firstsys.ToArray();
            List<string> tmp = x.ToList();
            tmp.Add(SYMBOL.PLUS.ToString());
            tmp.Add(SYMBOL.SUBTRACT.ToString());

            if (nowsymtype == SYMBOL.PLUS.ToString() || nowsymtype == SYMBOL.SUBTRACT.ToString())
            {
                symtmp = nowsymtype;//暂时记录首个符号是 + 还是 -
                if (!getsym())
                    return false;
                term(ref symtabindex, level, tmp);//表达式中的项分析
                if (symtmp == SYMBOL.SUBTRACT.ToString())//首个标识符是负号
                {
                    gen("OPR", 0, 1);
                }
            }
            else
            {
                term(ref symtabindex, level, tmp);//表达式中的项分析
            }
            while (nowsymtype == SYMBOL.PLUS.ToString() || nowsymtype == SYMBOL.SUBTRACT.ToString())//循环进行分析
            {
                symtmp = nowsymtype;
                if (!getsym())
                    return false;
                term(ref symtabindex, level, tmp);
                if (symtmp == SYMBOL.PLUS.ToString())//加号
                    gen("OPR", 0, 2);
                else
                    gen("OPR", 0, 3);
            }
            return true;
        }

        private bool term(ref int symtabindex, int level, List<string> firstsys)//项
        {
            string symtmp;
            var x = firstsys.ToArray();
            List<string> tmp = x.ToList();
            tmp.Add(SYMBOL.MULTIPLY.ToString());
            tmp.Add(SYMBOL.DIVIDE.ToString());
            factor(ref symtabindex, level, tmp);//项中的因子分析
            while (nowsymtype == SYMBOL.DIVIDE.ToString() || nowsymtype == SYMBOL.MULTIPLY.ToString())//循环进行项中的因子的分析
            {
                symtmp = nowsymtype;//记录当前符号为 * 还是 /
                if (!getsym())
                    return false;
                factor(ref symtabindex, level, tmp);
                if (symtmp == SYMBOL.MULTIPLY.ToString())
                    gen("OPR", 0, 4);
                else
                    gen("OPR", 0, 5);
            }
            return true;
        }

        private bool factor(ref int symtabindex, int level, List<string> firstsys)//因子
        {
            int i;
            if (!test(facbeginsym, firstsys, 24))//判断是否为表达式的开始符
                return false;
            while (facbeginsym.Contains(nowsymtype) || facbeginsym.Contains(nowsymname))
            {
                if (nowsymtype == SYMBOL.IDENTIFIER.ToString())//以标识符开始
                {
                    i = position(symtabindex, nowsymname);//判断标识符是否声明
                    if (i == 0)
                    {
                        //error no such identifier
                        grammarError(11);
                    }
                    else
                    {
                        switch (SymbolTab[i].Kind)//因子中的标识符必须为常量或变量
                        {
                            case "constant":
                                gen("LIT", 0, SymbolTab[i].Val);
                                break;
                            case "variable":
                                gen("LOD", level - SymbolTab[i].Level, SymbolTab[i].Addr);
                                break;
                            case "procedure":
                                //error here cannot be procedure
                                grammarError(21);
                                break;
                        }
                    }
                    if (!getsym())
                        return false;
                }
                else if (nowsymtype == SYMBOL.NUMBER.ToString())//以数字开始
                {
                    gen("LIT", 0, Convert.ToInt32(nowsymname));
                    if (!getsym())
                        return false;
                }
                else if (nowsymtype == SYMBOL.LEFTPARENTHESIS.ToString())//以左括号开始
                {
                    if (!getsym())
                        return false;
                    var x = firstsys.ToArray();//直接赋List1值给List2是浅拷贝，直接复制的地址
                    List<string> tmp = x.ToList();
                    tmp.Add(SYMBOL.RIGHTPARENTHESIS.ToString());
                    expression(ref symtabindex, level, tmp);//因子中左括号后的表达式分析
                    if (nowsymtype == SYMBOL.RIGHTPARENTHESIS.ToString())
                    {
                        if (!getsym())
                            return false;
                    }
                    else
                    {
                        //error here should be RightParenthesis
                        grammarError(22);
                    }
                }
                List<string> tmp2 = new List<string>();
                tmp2.Add(SYMBOL.LEFTPARENTHESIS.ToString());
                if (!test(firstsys, tmp2, 23))//判断是否为因子后的符号
                    return false;
            }
            return true;
        }

        private bool condition(ref int symtabindex, int level, List<string> firstsys)//条件
        {
            string symtmp;
            if (nowsymtype == SYMBOL.KEYWORD.ToString() && nowsymval == "odd")//为odd开始的条件
            {
                if (!getsym())
                    return false;
                expression(ref symtabindex, level, firstsys);//odd后的表达式
                gen("OPR", 0, 6);
            }
            else//表达式 关系运算符 表达式的形式
            {
                var x = firstsys.ToArray();
                List<string> tmp = x.ToList();
                tmp.Add(SYMBOL.EQUAL.ToString());
                tmp.Add(SYMBOL.NOTEQUAL.ToString());
                tmp.Add(SYMBOL.LESS.ToString());
                tmp.Add(SYMBOL.LESSANDEQUAL.ToString());
                tmp.Add(SYMBOL.GREATER.ToString());
                tmp.Add(SYMBOL.GREATERANDEQUAL.ToString());
                expression(ref symtabindex, level, tmp);//表达式分析

                /*string[] tmp = { SYMBOL.EQUAL.ToString(), SYMBOL.LESS.ToString(), SYMBOL.LESSANDEQUAL.ToString(),
                    SYMBOL.GREATER.ToString(), SYMBOL.GREATERANDEQUAL.ToString(), SYMBOL.NOTEQUAL.ToString()};*/
                if (!tmp.Contains(nowsymtype))
                {
                    //error should be 关系运算符
                    grammarError(20);
                }
                else
                {
                    symtmp = nowsymtype;//记录当前关系运算符
                    if (!getsym())
                        return false;
                    expression(ref symtabindex, level, firstsys);//关系运算符后的表达式分析
                    switch (symtmp)
                    {
                        case "EQUAL":
                            gen("OPR", 0, 8);
                            break;
                        case "NOTEQUAL":
                            gen("OPR", 0, 9);
                            break;
                        case "LESS":
                            gen("OPR", 0, 10);
                            break;
                        case "GREATERANDEQUAL":
                            gen("OPR", 0, 11);
                            break;
                        case "GREATER":
                            gen("OPR", 0, 12);
                            break;
                        case "LESSANDEQUAL":
                            gen("OPR", 0, 13);
                            break;
                    }
                }
            }
            return true;
        }

        //解释器
        private int startInterpret(string[] input)
        {
            int[] sta = new int[305];
            for (int i = 0; i < 305; i++)
                sta[i] = 0;
            int pc = 0, bp = 0, sp = 0;//stackpointer
            int breaknum = 50000;
            do
            {
                PCodeTable currentCode = PCodeList[pc++];
                switch (currentCode.Operation)
                {
                    case "LIT"://取常量置于栈顶
                        sta[sp++] = currentCode.Address;
                        break;
                    case "OPR"://计算
                        switch (currentCode.Address)
                        {
                            case 0://函数调用返回
                                sp = bp;
                                pc = sta[sp + 2];
                                bp = sta[sp + 1];
                                break;
                            case 1://负号
                                sta[sp - 1] = -sta[sp - 1];
                                break;
                            case 2://加
                                sp--;
                                sta[sp - 1] += sta[sp];
                                break;
                            case 3://减
                                sp--;
                                sta[sp - 1] -= sta[sp];
                                break;
                            case 4://乘
                                sp--;
                                sta[sp - 1] = sta[sp - 1] * sta[sp];
                                break;
                            case 5://除
                                sp--;
                                sta[sp - 1] /= sta[sp];
                                break;
                            case 6://奇偶判断
                                sta[sp - 1] %= 2;
                                break;
                            case 8://判断相等
                                sp--;
                                sta[sp - 1] = (sta[sp] == sta[sp - 1] ? 1 : 0);
                                break;
                            case 9://判断不等
                                sp--;
                                sta[sp - 1] = (sta[sp] != sta[sp - 1] ? 1 : 0);
                                break;
                            case 10://判断小于
                                sp--;
                                sta[sp - 1] = (sta[sp - 1] < sta[sp] ? 1 : 0);
                                break;
                            case 11://判断大于等于
                                sp--;
                                sta[sp - 1] = (sta[sp - 1] >= sta[sp] ? 1 : 0);
                                break;
                            case 12://判断大于
                                sp--;
                                sta[sp - 1] = (sta[sp - 1] > sta[sp] ? 1 : 0);
                                break;
                            case 13://判断小于等于
                                sp--;
                                sta[sp - 1] = (sta[sp - 1] <= sta[sp] ? 1 : 0);
                                break;

                        }
                        break;
                    case "LOD"://取变量置于栈顶
                        sta[sp] = sta[Base(currentCode.Layer, sta, bp) + currentCode.Address];
                        sp++;
                        break;
                    case "STO"://栈顶值存于变量
                        sp--;
                        sta[Base(currentCode.Layer, sta, bp) + currentCode.Address] = sta[sp];
                        break;
                    case "CAL"://调用过程
                        sta[sp] = Base(currentCode.Layer, sta, bp);
                        sta[sp + 1] = bp;
                        sta[sp + 2] = pc;
                        bp = sp;
                        pc = currentCode.Address;
                        break;
                    case "INT"://分配空间，指针+Address
                        sp += currentCode.Address;
                        break;
                    case "JMP"://无条件跳转至Address
                        pc = currentCode.Address;
                        break;
                    case "JPC"://条件跳转至Address
                        sp--;
                        if (sta[sp] == 0)
                        {
                            pc = currentCode.Address;
                        }
                        break;
                    case "RED"://读数据
                        int tmp;
                        try {
                            tmp = Convert.ToInt32(input[inputtop++]);
                        }catch(Exception e)
                        {
                            //MessageBox.Show(e.ToString(), "Error");
                            return 1;
                        }
                        sta[sp] = tmp;
                        sta[Base(currentCode.Layer, sta, bp) + currentCode.Address] = sta[sp];
                        break;
                    case "WRT":
                        OutputTextBox.Text += sta[sp - 1].ToString();
                        OutputTextBox.Text += Environment.NewLine;
                        sp--;
                        break;
                }
                breaknum--;
                if(breaknum<=0)
                {
                    return 2;
                }
            } while (pc != 0);
            return 0;
        }

        //通过过程基址求上一层过程基址
        private int Base(int l, int[] sta, int bp)
        {
            while (l > 0)
            {
                bp = sta[bp];
                l--;
            }
            return bp;
        }

        //关于可视化交互
        private void Check_Click(object sender, RoutedEventArgs e)//点击选择文件按钮
        {
            //清空词汇表及其显示
            this.SymbolList.Clear();
            this.SymbolTableView.ItemsSource = SymbolList;

            txt.Clear();
            this.fileText.Clear();

            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                //Filter = "Text Files (*.txt)|*.txt"
                Filter = "All Files (*.*)|*.*"
            };
            var result = openFileDialog.ShowDialog();
            if (result == true)
            {
                this.Filepath.Text = openFileDialog.FileName;
            }

            //清空各个文本框
            ErrorText.Clear();
            PCodeList.Clear();
            PCode_ListView.ItemsSource = PCodeList;
            Interpret_Btn.IsEnabled = false;
            InputTextBox.Text = String.Empty;
            OutputTextBox.Text = String.Empty;
        }

        private void fileSure_Click(object sender, RoutedEventArgs e)//确认文件，显示到文本框
        {
            Interpret_Btn.IsEnabled = false;
            InputTextBox.Text = String.Empty;
            OutputTextBox.Text = String.Empty;

            PCodeList.Clear();
            PCode_ListView.ItemsSource = PCodeList;
            ErrorText.Clear();
            //清空词汇表及其显示
            this.SymbolList.Clear();
            this.SymbolTableView.ItemsSource = SymbolList;

            txt.Clear();
            this.fileText.Clear();
            string filepath = this.Filepath.Text;
            if (filepath.Length > 0)
            {
                try
                {
                    System.IO.StreamReader streamReader = new System.IO.StreamReader(filepath, Encoding.Default);
                    while (streamReader.Peek() > 0)
                    {
                        string temp = streamReader.ReadLine();
                        //temp = temp.TrimStart();//去掉首部空格
                        txt.Add(temp);
                    }
                    int linenum = 0;
                    foreach (var item in txt)
                    {
                        linenum++;
                        if (linenum != 1)
                            this.fileText.Text += '\n';
                        string tmp = string.Format("{0, 3}| ",linenum);//增加行号显示
                        this.fileText.Text += tmp;
                        this.fileText.Text += item;
                    }
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message, "Error");
                }
            }
            else
            {
                MessageBox.Show("文件名不能为空", "Error");
            }
        }

        private void Analysis_Click(object sender, RoutedEventArgs e)//点击编译按钮
        {
            this.SymbolList.Clear();
            this.SymbolTableView.ItemsSource = SymbolList;
            this.ErrorText.Clear();
            line = 0;
            errornum = 0;
            foreach (var item in txt)
            {
                //string tmp = item + '#';
                string tmp = item.TrimStart() + '#';
                line++;
                startAnalysis(tmp);
            }
            //SymbolList.Add(new SymbolTable("a", "b", "c"));
            //this.SymbolTableView.ItemsSource = SymbolList;
            /*Just test copy List a to List b
            List<string> x = new List<string>();
            x.Add("1");
            x.Add("2");
            var z = x.ToArray();
            List<string> y = z.ToList();
            x[0] = "3";
            MessageBox.Show(y[0].ToString());*/
            if(SymbolList.Count>0)
                startGrammaAnalysis();//开始语法分析
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            Filepath.Text = "";
            var openFileDialog = new Microsoft.Win32.OpenFileDialog()
            {
                //Filter = "Text Files (*.txt)|*.txt"
                Filter = "All Files (*.*)|*.*"
            };
            var result = openFileDialog.ShowDialog();
            if (result == true)
            {
                this.Filepath.Text = openFileDialog.FileName;
            }
            this.Filepath.SelectionStart = Filepath.Text.Length;
        }

        private void Interpret_Btn_Click(object sender, RoutedEventArgs e)
        {
            OutputTextBox.Text = String.Empty;

            if (fileText.Text.Trim() != String.Empty)
            {
                if (PCodeList.Count != 0)
                {
                    if (errornum == 0 && gerrorcnt == 0)
                    {
                        inputtop = 0;
                        string tmp = InputTextBox.Text;
                        input = tmp.Split(new char[] { ',', ' ', '\t', '\n' }, StringSplitOptions.RemoveEmptyEntries);
                        int interpretresult = startInterpret(input);
                        if(interpretresult ==0)
                        {
                            OutputTextBox.Text += Environment.NewLine;
                            OutputTextBox.Text += "解释成功";
                        }
                        else if (interpretresult == 1)
                            MessageBox.Show("程序需要进行输入，当前没有输入数据或输入数据不够", "Error");
                        else if (interpretresult == 2)
                            MessageBox.Show("程序可能出现死循环，请检查程序", "Error");
                    }
                    else
                    {
                        MessageBox.Show("编译失败，无法进行解释", "Error");
                    }
                }
                else
                    MessageBox.Show("还未完成编译，无法进行解释", "Error");
            }
            else
                MessageBox.Show("当前程序为空", "Error");
        }
    }
}
