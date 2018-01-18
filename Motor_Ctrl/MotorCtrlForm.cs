
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using gts;


namespace Motor_Ctrl
{
    public partial class MotorCtrlForm : Form
    {
        private string _cTime; // 日志记录的当前时间

        public double[] SensCorPar = new double[3] {1 / 6.23, -1 / 0.623, -1 / 0.623 }; // 传感器修正系数（编码器, 光栅, 磁栅）
        public double[] SensBiasPar = new double[3] {0, 0, 0}; // 传感器修正偏置量（编码器, 光栅, 磁栅）

        public double PosLimitMax = 78; // 左软限位
        public double PosLimitMin = -8; // 右软限位

        public double VelLimitMin = 0.2; // 速度限制最小值
        public double VelLimitMax = 20; // 速度限制最大值

        public double HandyVel = 5; // 手动速度

        public double pul2mmCoef = 0.0005;// TransPar=tr/pr mm/pulse

        public bool StepModeIsDir = false; // 方向+脉冲模式
        
        public bool HomePosStatus;
        public double GrenzPosOfHome;

        public int CurrEncoder = 0; // 传感器选择 0>编码器 1>光栅尺 2>磁栅尺 
        public static short AxisStopSmooth = 0; // 用于GT_Stop, 平滑停止
        public static short AxisStopHard = 1; // 用于GT_Stop, 急停
        public short AxisNo = 1;

        public int GnStatus = 4095; // BIN: 11111111111
        public bool MotorEnIsOn = false; // 电机使能开关
        public bool DriverAlarmEn = true; // 驱动器警报使能
        public bool MinLmtAlarmEn = true; // min限位开关警报使能
        public bool MaxLmtAlarmEn = true; // max限位开关警报使能

        public bool MinAlarmTriggered = false; // 负限位开关触发
        public bool MaxAlarmTriggered = false; // 正限位开关触发

        public bool SettingChanged = false; // 手动模式上面的几个文本框改变

        public bool CtrlIsOn = false; // 控制器开关
        public int Unit = 1; // 单位 0->mm 1->pulse
        public int PulsePos = 0; // 由 GetPos 得到的 pulse 位置
        public double PrfPos = 0; // TODO 规划位置 暂时不知道有什么用，定时器任务 获取规划位置

        public bool HandyFowardMouseDown = false; // 手动正向按钮按下
        public bool HandyBackMouseDown = false; // 手动负向按钮按下

        public double AutoAimIncre = 0; // 自动模式，增量位置
        public double AutoAimAbs = 0; // 自动模式，绝对位置

        public int ModeActivated = 0; // 0 -> JogMode, 1 -> TrapMode

        public bool MotorIsMoving = false; // 电机在运动

        public double HomePos = 0; // 原点位置 单位 Pulse
        public double EncPos = 0; // 编码器读到的位置（比如，对于编码器 10000 Pulse 读到的是62000）
        public double RelPos = 0; // 相对位置 = 绝对位置 - 原点位置 单位 Pulse
        public double AbsPos = 0; // 绝对位置 单位 Pulse
        public double AbsPos0 = 0; // 上一次的绝对位置，用于求 CurrVel 当前速度 单位 Pulse
        public double CurrVel = 0; // 当前速度 单位 Pulse/ms

        public bool SoftLimit = true; // 软限位打开
        public bool VelLimit = true; // 速度限制打开

        public bool MinSoftLimitAct = false;
        public bool MaxSoftLimitAct = false; // 软限位激活标志

        public bool IncreModeActivated = true; // 与此相对的是 AbsModeActivated(IncreModeActivated = false)

        public gts.mc.TJogPrm TJog = new gts.mc.TJogPrm();
        // TJog 参数 acc 加速度 dec 减速度 smooth平滑系数 [0,1）越大越平稳
        public gts.mc.TTrapPrm TTrap = new gts.mc.TTrapPrm();
        // TTrap 参数 acc 加速度 dec 减速度 smoothTime平滑时间 = (short)(TJog.smooth * 50) 根据TJog 的平滑系数来换算
        public gts.mc.TCrdPrm TCrd = new gts.mc.TCrdPrm();
        // TCrd 参数

        public bool MouseMovement = false;

        public double VelGet = 0; // TODO 临时性加的。

        public struct InterLine // 插补线结构体
        {
            public short crd; // 插补段的坐标系
            public int x; // 插补段的终点坐标x
            public int y; // 插补段的终点坐标y
            public double SynVel; // 插补段的目标速度
            public double SynAcc; // 插补段的加速度
            public double EndVel; // 终点速度, 一般是0（不用前瞻预处理）
            public short fifo; // 插补缓存区号[0,1]
        }

        public InterLine line1 = new InterLine();

        public short CrdRun = 0;
        public int CrdSegment = 0;

        /*************************************************************************************************************/

        public void UI_Init()
        {
            Motor_Btn.Enabled = false;
            label2.Enabled = false;
            Alarm_Gbx.Enabled = false;
            FindZero_Btn.Enabled = false;
            SetCrtPos2Zero_Btn.Enabled = false;
            PosVel_Gbx.Enabled = false;
            Select_Gbx.Enabled = false;
            
            PosLmt_Lb.Text = "[" + PosLimitMin.ToString() + "," + PosLimitMax.ToString() + "]";
            VelLmt_Lb.Text = "[" + VelLimitMin.ToString() + "," + VelLimitMax.ToString() + "]";

            SensorSelect_Cbb.SelectedIndex = 0;
            UnitSelect_Cbb.SelectedIndex = 1;

            LnrAcc_Tb.Text = TJog.acc.ToString();
            LnrDec_Tb.Text = TJog.dec.ToString();
            LnrSmCoef_Tb.Text = TJog.smooth.ToString();
            LnrVel_Tb.Text = HandyVel.ToString();
            LnrParmSet_Btm.BackColor = Color.LightGreen;

            LnrIncrePos_Tb.Text = AutoAimIncre.ToString();
            LnrAbsPos_Tb.Text = AutoAimAbs.ToString();

            LnrAbsPos_Tb.Enabled = false;
            AutoMode_Gbx.Enabled = false;
            LinearMotion_Gbx.Enabled = false;
            CircMotion_Gbx.Enabled = false;

        }

        public void UI_Reset() // 重置UI
        {  
            Motor_Btn.Enabled = false;
            label2.Enabled = false;
            Alarm_Gbx.Enabled = false;
            FindZero_Btn.Enabled = false;
            SetCrtPos2Zero_Btn.Enabled = false;
            PosVel_Gbx.Enabled = false;
            Select_Gbx.Enabled = false;
            LinearMotion_Gbx.Enabled = false;
            CircMotion_Gbx.Enabled = false;
            // 电机使能相关
            Motor_status.BackColor = Color.WhiteSmoke;
            Motor_Btn.Text = "开启电机使能";
            //SensorSelect_Cbb.SelectedIndex = 0;
            //UnitSelect_Cbb.SelectedIndex = 1;
        }

        public void UI_CtrlOn() // UI在连接上控制器时的状态
        {
            Motor_Btn.Enabled = true;
            label2.Enabled = true;
            Alarm_Gbx.Enabled = true;
            FindZero_Btn.Enabled = true;
            SetCrtPos2Zero_Btn.Enabled = true;
            PosVel_Gbx.Enabled = true;
            Select_Gbx.Enabled = true;
            LinearMotion_Gbx.Enabled = true;
            //CircMotion_Gbx.Enabled = false;

            //SensorSelect_Cbb.SelectedIndex = 0;
            //UnitSelect_Cbb.SelectedIndex = 1;
            MotionPtn_Cbb.SelectedIndex = 0;
        }

        public void Init() // 对一些变量赋初值
        {
            // TJog 参数初始化
            {
                HandyVel = 5; // 不在结构体之内
                TJog.acc = 1.0;
                TJog.dec = 1.0;
                TJog.smooth = 0.3; // 平滑系数 [0,1）越大越平稳
            }
            // TTrap 参数初始化
            {
                TTrap.acc = 1.0;
                TTrap.dec = 1.0;
                TTrap.velStart = 0; // 起跳速度
                TTrap.smoothTime = (short) (TJog.smooth * 50); // 平滑时间
            }
            // TCrd 参数初始化
            {
                TCrd.dimension = 2; // 坐标系为二维坐标系
                TCrd.synVelMax = 20; // 最大合成速度： 20 pulse/ms
                TCrd.synAccMax = 10; // 最大加速度： 10 pulse/ms^2
                TCrd.evenTime = 50; // 最小匀速时间： 50ms

                TCrd.profile1 = 1; // 规划器1对应到X轴
                TCrd.profile2 = 2; // 规划器2对应到Y轴
                TCrd.setOriginFlag = 1; // 表示需要指定坐标系的原点坐标的规划位置
                TCrd.originPos1 = 30000; // 坐标系的原点坐标的规划位置为（30000, 30000）
                TCrd.originPos2 = 30000; // 坐标系的原点坐标的规划位置为（30000,30000）
            }
            // line1 参数初始化
            {
                line1.crd = 1; // 插补段的坐标系
                line1.x = 0; // 插补段的终点坐标x
                line1.y = 0; // 插补段的终点坐标y
                line1.SynVel = 3; // 插补段的目标速度
                line1.SynAcc = 1; // 插补段的加速度
                line1.EndVel = 0; // 终点速度, 一般是0（不用前瞻预处理）
                line1.fifo = 0; // 插补缓存区号[0,1]

            }
        }

        public void InitTemp()
        {
            MaxLmtEnOff(2);
            MinLmtEnOff(2);
            DriverAlarmEnOff(2);
            MotorSetOn(2);
        }

        public void Reset() // 重置变量和逻辑
        {

        }

        public void PreInitAfterCtrlOn()
        {
            StopAxis(AxisNo, AxisStopSmooth); // 停止轴运动
            MotorSetOff(AxisNo); // 关闭电机使能
            CtrlReset(); // 控制器复位
            DriverAlarmEnOn(AxisNo); // 驱动器报警使能开
            MaxLmtEnOn(AxisNo); // 正限位报警开
            MinLmtEnOn(AxisNo); // 负限位报警开
            JogModeAct(); // 预先加载Jog模式
            GetStatus_Tmr.Enabled = true; // 计时器-> 获取系统状态 使能开
            HandyMode_Tmr.Enabled = true;
        }

        public double pulse2mm(double pulse)
        {
            return pulse * pul2mmCoef;
        }

        public double pulse2pulse(double pulse)
        {
            return pulse;
        }
        
        public void UI_mm_cvt_pulse()
        {
            
            string PosUnit = "pulse";
            string VelUnit = "pulse/ms";
            string AccUnit = "pulse/ms2";

            PosUnit_Lb1.Text = PosUnit;
            PosUnit_Lb2.Text = PosUnit;
            PosUnit_Lb3.Text = PosUnit;
            PosUnit_Lb4.Text = PosUnit;
            PosUnit_Lb5.Text = PosUnit;

            VelUnit_Lb1.Text = VelUnit;
            VelUnit_Lb2.Text = VelUnit;

            AccUnit_Lb1.Text = AccUnit;
            AccUnit_Lb2.Text = AccUnit;

        }

        public void UI_pulse_cvt_mm()
        {
            string PosUnit = "mm";
            string VelUnit = "mm/ms";
            string AccUnit = "mm/ms2";

            PosUnit_Lb1.Text = PosUnit;
            PosUnit_Lb2.Text = PosUnit;
            PosUnit_Lb3.Text = PosUnit;
            PosUnit_Lb4.Text = PosUnit;
            PosUnit_Lb5.Text = PosUnit;

            VelUnit_Lb1.Text = VelUnit;
            VelUnit_Lb2.Text = VelUnit;       

            AccUnit_Lb1.Text = AccUnit;
            AccUnit_Lb2.Text = AccUnit;
        }

        public int StopAxis(short AxisNo, short ModeOfStop) //停止运动
        {
            int sRtn = 10;
            sRtn = gts.mc.GT_Stop(1 << (AxisNo - 1), ModeOfStop);

            return sRtn;
        }

        public short CtrlOpen()
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_Open(0, 0);
            if (sRtn == 0)
            {
                ActRecord("CtrlOpen Ok");
            }
            else
            {
                ActRecord("CtrlOpen Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short CtrlClose()
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_Close();
            if (sRtn == 0)
            {
                ActRecord("CtrlClose Ok");
            }
            else
            {
                ActRecord("CtrlClose Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short CtrlReset()
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_Reset();
            if (sRtn == 0)
            {
                ActRecord("CtrlReset Ok");
            }
            else
            {
                ActRecord("CtrlReset Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short MotorSetOn(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_AxisOn(AxisNo);
            if (sRtn == 0)
            {
                ActRecord("MotorSetOn Ok");
                MotorEnIsOn = true;
            }
            else
            {
                ActRecord("MotorSetOn Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short MotorSetOff(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_AxisOff(AxisNo);
            if (sRtn == 0)
            {
                ActRecord("MotorSetOff Ok");
                MotorEnIsOn = false;
            }
            else
            {
                ActRecord("MotorSetOff Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short DriverAlarmEnOn(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_AlarmOn(AxisNo);
            if (sRtn == 0)
            {
                ActRecord("DriverAlarmEnOn Ok");
            }
            else
            {
                ActRecord("DriverAlarmEnOn Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short DriverAlarmEnOff(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_AlarmOff(AxisNo);
            if (sRtn == 0)
            {
                ActRecord("DriverAlarmEnOff Ok");
            }
            else
            {
                ActRecord("DriverAlarmEnOff Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short MaxLmtEnOn(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_LmtsOn(AxisNo, 0);
            if (sRtn == 0)
            {
                ActRecord("MaxLmtEnOn Ok");
            }
            else
            {
                ActRecord("MaxLmtEnOn Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short MaxLmtEnOff(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_LmtsOff(AxisNo, 0);
            if (sRtn == 0)
            {
                ActRecord("MaxLmtEnOff Ok");
            }
            else
            {
                ActRecord("MaxLmtEnOff Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short MinLmtEnOn(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_LmtsOn(AxisNo, 1);
            if (sRtn == 0)
            {
                ActRecord("MinLmtEnOn Ok");
            }
            else
            {
                ActRecord("MinLmtEnOn Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short MinLmtEnOff(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_LmtsOff(AxisNo, 1);
            if (sRtn == 0)
            {
                ActRecord("MinLmtEnOff Ok");
            }
            else
            {
                ActRecord("MinLmtEnOff Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short SetStepMode2Dir(short AxisNo)
        {
            short sRtn = -5;
            sRtn = gts.mc.GT_StepDir(AxisNo);
            if (sRtn == 0)
            {
                ActRecord("SetStepMode2Dir Ok");

            }
            else
            {
                ActRecord("SetStepMode2Dir Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short SetStepMode2Pulse(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_StepPulse(AxisNo);
            if (sRtn == 0)
            {
                ActRecord("SetStepMode2Pulse Ok");
               
            }
            else
            {
                ActRecord("SetStepMode2Pulse Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short ClearStatusAxis(short AxisNo, short n = 1)  //状态清除 起始轴,清零轴数
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_ClrSts(AxisNo, n);
            if (sRtn == 0)
            {
                //RecordAct("ClearStatusAxis done");
            }
            else
            {
                ActRecord("ClearStatusAxis Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short GetStatusAxis(short AxisNo)
        {
            short sRtn = 10;
            int AxisStatus, TempAxisStatus, TempGnstatus;
            int Scount = 0, ASi, GSi;
            uint pClock;
            sRtn = gts.mc.GT_GetSts(AxisNo, out AxisStatus, 1, out pClock);
            TempGnstatus = GnStatus;
            TempAxisStatus = AxisStatus;
            if (sRtn == 0)
            {
                while (TempAxisStatus != TempGnstatus)
                {
                    ASi = TempAxisStatus & 1;
                    GSi = TempGnstatus & 1;
                    //if (ASi != GSi)
                    {
                        if (Scount == 1)//5-2 轴状态定义 驱动器报警 
                        {
                            if (ASi == 1)
                            {
                                Driver_Status.BackColor = Color.Red;
                            }
                            else
                            {
                                Driver_Status.BackColor = DriverAlarmEn ? Color.Lime : Color.WhiteSmoke;
                            }
                        }
                        if (Scount == 5)//5-2 轴状态定义 正限位报警 
                        {
                            if (ASi == 1)
                            {
                                MaxLmt_Status.BackColor = Color.Red;
                                MaxAlarmTriggered = true;
                            }
                            else
                            {
                                MaxLmt_Status.BackColor = MaxLmtAlarmEn ? Color.Lime : Color.WhiteSmoke;
                            }
                        }
                        if (Scount == 6)//5-2 轴状态定义 负限位报警
                        {
                            if (ASi == 1)
                            {
                                MinLmt_Status.BackColor = Color.Red;
                                MinAlarmTriggered = true;
                            }
                            else
                            {
                                MinLmt_Status.BackColor = MinLmtAlarmEn ? Color.Lime : Color.WhiteSmoke;
                            }
                        }
                        //if (Scount == 9)//5-2 
                        //{
                        //    if (ASi == 1)
                        //    {
                        //        MotorStatusLb.Text = "On";
                        //        MotorStatusLb.ForeColor = ClGreen;
                        //    }
                        //    else
                        //    {
                        //        MotorStatusLb.Text = "Off";
                        //        MotorStatusLb.ForeColor = ClRed;
                        //    }
                        //}
                        if (Scount == 10) // 规划运动 
                        {
                            if (ASi == 1)
                            {
                                MotorIsMoving = true;
                                Motor_status.BackColor = Color.BlueViolet;
                            }
                            else
                            {
                                MotorIsMoving = false;
                                Motor_status.BackColor = MotorEnIsOn ? Color.Lime : Color.WhiteSmoke;
                            }
                        }
                    }
                    Scount += 1;
                    TempAxisStatus = TempAxisStatus >> 1;
                    TempGnstatus = TempGnstatus >> 1;
                }
                GnStatus = AxisStatus;

            }
            else
            {
                ActRecord("GetStatusAxis error |code " + sRtn.ToString());
                GetStatus_Tmr.Enabled = false;
            }
            return 0;
        }

        public int GetPrfModeOfAxis(short AxisNo)
        {
            short sRtn = 10;
            int pValue;
            uint uClock;
            sRtn = gts.mc.GT_GetPrfMode(AxisNo, out pValue, 1, out uClock);//规划轴运动模式
            if (sRtn == 0)
            {
                return pValue;
            }
            else
            {
                ActRecord("GetPrfModeOfAxis Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short Set2ZeroPoint(short AxisNo, short n = 1)//将当前点设置为零点 起始轴,清零轴数
        {
            short sRtn = 0;
            sRtn = gts.mc.GT_ZeroPos(AxisNo, n);
            if (sRtn == 0)
            {
                HomePos = (CurrEncoder == 0) ? 0 : GetPosFromSensor(AxisNo);
                ActRecord("SetZeroPos Ok");
            }
            else
            {
                ActRecord("Set2ZeroPoint Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short SetPrfPos(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_SetPrfPos(AxisNo, 0);
            if (sRtn == 0)
            {
            }
            else
            {
                ActRecord("SetPrfPos Error |code " + sRtn.ToString());
            }

            return sRtn;
        }

        //*****Jog Mode*************************************// 
        public short SetPrf2Jog(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_PrfJog(AxisNo);
            if (sRtn == 0)
            {
                //ModeActivated = 0;
            }
            return sRtn;

        }

        public short SetParmOfJog(short AxisNo, gts.mc.TJogPrm TJogPrm)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_SetJogPrm(AxisNo, ref TJogPrm);       
            return sRtn;
        }

        public short SetVel(short AxisNo, double v)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_SetVel(AxisNo, v);
            if (sRtn == 0)
            {
                ActRecord("SetVel ok " + v.ToString());
            }
            else
            {
                ActRecord("SetVel Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short GetVel(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_GetVel(AxisNo, out VelGet);
            if (sRtn == 0)
            {
                ActRecord("GetVel ok " + VelGet.ToString());
            }
            else
            {
                ActRecord("GetVel Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public int JogModeAct()
        {
            StopAxis(AxisNo, AxisStopHard);
            if (SetPrf2Jog(AxisNo) == 0)
            {
                SetVel(AxisNo, HandyVel);
                SetParmOfJog(AxisNo, TJog);
            }
            ActRecord("Jog Mode Activited");
            return 0;
        }

        public void TJogUpdateFromUI() // 只用于手动自动相互切换的时候，传递一下参数
        {
            HandyVel = double.Parse(LnrVel_Tb.Text);
            TJog.acc = double.Parse(LnrAcc_Tb.Text);
            TJog.dec = double.Parse(LnrDec_Tb.Text);
            TJog.smooth = double.Parse(LnrSmCoef_Tb.Text);
        }

        //*****Trap Mode*************************************// 
        public short SetPrf2Trap(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_PrfTrap(AxisNo);
            if (sRtn == 0)
            {
                //ModeActivated = 1;
            }
            return sRtn;
        }

        public short SetParmOfTrap(short AxisNo, gts.mc.TTrapPrm TTrap)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_SetTrapPrm(AxisNo, ref TTrap);
            return sRtn;
        }

        public int TrapModeAct()
        {
            StopAxis(AxisNo, AxisStopHard);
            if (SetPrf2Trap(AxisNo) == 0)
            {
                SetVel(AxisNo, HandyVel);
                SetParmOfTrap(AxisNo, TTrap);
            }
            ActRecord("Trap Mode Activited");
            return 0;
        }

        public short GetPos(short AxisNo, out int PulsePos)
        {
            short sRtn = 0;
            sRtn = gts.mc.GT_GetPos(AxisNo, out PulsePos);
            if (sRtn == 0)
            { 
            }
            else
            {
                ActRecord("SetPos Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short SetPos(short AxisNo, int pulse)
        {
            short sRtn = 0;
            sRtn = gts.mc.GT_SetPos(AxisNo, pulse);
            if (sRtn == 0)
            {              
                ActRecord("SetPos to "+ pulse.ToString());
            }
            else
            {
                ActRecord("SetPos Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public void TTrapUpdateFromUI() // 只用于手动自动相互切换的时候，传递一下参数
        {
            HandyVel = double.Parse(LnrVel_Tb.Text);
            TTrap.acc = double.Parse(LnrAcc_Tb.Text);
            TTrap.dec = double.Parse(LnrDec_Tb.Text);
            TTrap.smoothTime = (short)(double.Parse(LnrSmCoef_Tb.Text) * 50);
        }

        /*******Crd Mode***************************************************/
        public short CrdFifoClear()
        {
            short sRtn = 0;
            sRtn = gts.mc.GT_CrdClear(1, 0);
            if (sRtn == 0)
            {
                ActRecord("CrdFifoClear Ok");
            }
            else
            {
                ActRecord("CrdFifoClear Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short SetCrdPrm()
        {
            short sRtn = 0;
            sRtn = gts.mc.GT_SetCrdPrm(1, ref TCrd);
            if (sRtn == 0)
            {
                ActRecord("SetCrdPrm Ok");
            }
            else
            {
                ActRecord("SetCrdPrm |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short LnXY(InterLine line)
        {
            short sRtn = 0;
            sRtn = gts.mc.GT_LnXY(line.crd, line.x, line.y, line.SynVel, line.SynAcc, line.EndVel, line.fifo);
            if (sRtn == 0)
            {
                ActRecord("LnXY Ok");
            }
            else
            {
                ActRecord("LnXY |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short GetCrdSpace(out int space)
        {
            short sRtn = 0;
            sRtn = gts.mc.GT_CrdSpace(1, out space, 0);
            if (sRtn == 0)
            {
                ActRecord("GetCrdSpace Ok");
            }
            else
            {
                ActRecord("GetCrdSpace |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short GetCrdStatus(out short run,out int segment)
        {
            short sRtn = 0;
            sRtn = gts.mc.GT_CrdStatus(1, out run, out segment, 0);
            if (sRtn == 0)
            {
                ActRecord("GetCrdStatus Ok");
            }
            else
            {
                ActRecord("GetCrdStatus |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public short CrdStart()
        {
            short sRtn = 0;
            sRtn = gts.mc.GT_CrdStart(1, 0);
            if (sRtn == 0)
            {
                ActRecord("CrdStart Ok");
            }
            else
            {
                ActRecord("CrdStart |code " + sRtn.ToString());
            }
            return sRtn;
        }

        /******************************************************************/

        public double GetPosFromSensor(short AxisNo)
        {
            int sRtn = 10;
            double pValue;
            uint pClock;
            AbsPos0 = AbsPos; // 记录上一次的位置
            sRtn = gts.mc.GT_GetEncPos(AxisNo, out pValue, 1, out pClock);
            if (sRtn == 0)
            {
                EncPos = pValue;

                AbsPos = pValue * SensCorPar[CurrEncoder] + SensBiasPar[CurrEncoder];
                RelPos = AbsPos - HomePos;
                return AbsPos;
            }
            else
            {
                ActRecord("GetPosFromSensor Error |code " + sRtn.ToString());
            }
            return 0;
        }

        public short CommandUpdate(short AxisNo)
        {
            short sRtn = 10;
            sRtn = gts.mc.GT_Update(1 << (AxisNo - 1));
            if (sRtn == 0)
            {

            }
            else
            {
                ActRecord("CommandUpdate Error |code " + sRtn.ToString());
            }
            return sRtn;
        }

        public void HandyGoForward(double HG_Vel)
        {

            CheckMotorIsOnDlg();

            SetVel(AxisNo, HG_Vel); // 速度是正的
            //GetVel(AxisNo);
            CommandUpdate(AxisNo);
            if (MaxAlarmTriggered) StopAxis(AxisNo, AxisStopHard);
            if (MinAlarmTriggered) ClearStatusAxis(AxisNo);
        }

        public void HandyGoBackward(double HG_Vel)
        {

            CheckMotorIsOnDlg();

            SetVel(AxisNo, -(HG_Vel)); // 速度是负的
            //GetVel(AxisNo);
            CommandUpdate(AxisNo);

            if (MinAlarmTriggered) StopAxis(AxisNo, AxisStopHard);
            if (MaxAlarmTriggered) ClearStatusAxis(AxisNo);
        }

        public bool CheckHomePos()
        {
            short sRtn = 10;
            int pValue;
            bool ArrivedHome_Temp;
            sRtn = gts.mc.GT_GetDi(gts.mc.MC_HOME, out pValue);
            if (sRtn == 0)
            {
                if ((pValue & (1 << 0)) > 0)
                {
                    ArrivedHome_Temp = false;
                }
                else
                {
                    ArrivedHome_Temp = true;
                }
                if (ArrivedHome_Temp != HomePosStatus)
                {
                    HomePosStatus = ArrivedHome_Temp;
                    GrenzPosOfHome = GetPosFromSensor(AxisNo);
                }
                return ArrivedHome_Temp;
            }
            else
            {
            }
            return false;
        }

        public void GoFindHome()
        {
            DialogResult res = MessageBox.Show("请确认是否已连接外部传感器", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (res == DialogResult.Yes)
            {
            }
            else
            {
                return;
            }

            CheckMotorIsOnDlg();

            StopAxis(AxisNo, AxisStopSmooth);
            bool isNowHome = false; // 判断是否初始值是零点
            isNowHome = CheckHomePos();
            double HandyVel_Temp = HandyVel;
            if (isNowHome) //已经停在原点上了
            {
                HandyGoForward(HandyVel);//S1
                while (true)
                {
                    Application.DoEvents();
                    if (!CheckHomePos()) { StopAxis(AxisNo, AxisStopHard); break; }
                    if (MaxAlarmTriggered) { MessageBox.Show("请检查原点开关安装", "Warning"); StopAxis(AxisNo, AxisStopHard); return; };
                }
                HandyGoBackward(VelLimitMin);
                while (!CheckHomePos()) Application.DoEvents();
                StopAxis(AxisNo, AxisStopHard);
                HandyGoForward(VelLimitMin);
                while (CheckHomePos()) Application.DoEvents();
                StopAxis(AxisNo, AxisStopHard);
                HandyGoBackward(VelLimitMin);
                while (!CheckHomePos()) Application.DoEvents();
                StopAxis(AxisNo, AxisStopHard);
                //MotorSetOff(AxisNo); // TODO 关电机？
            }
            else
            {
                HandyGoBackward(HandyVel);
                while (true)
                {
                    Application.DoEvents();
                    if (CheckHomePos()) { StopAxis(AxisNo, AxisStopHard); break; }
                    if (MinAlarmTriggered) { StopAxis(AxisNo, AxisStopHard); break; };
                }
                if (MinAlarmTriggered)
                {
                    HandyGoForward(HandyVel);
                    while (!CheckHomePos()) Application.DoEvents();
                    while (CheckHomePos()) Application.DoEvents();
                    HandyGoBackward(VelLimitMin);
                    while (!CheckHomePos()) Application.DoEvents();
                    StopAxis(AxisNo, AxisStopHard);
                }
                HandyGoForward(VelLimitMin);
                while (CheckHomePos()) Application.DoEvents();
                StopAxis(AxisNo, AxisStopHard);
                HandyGoBackward(VelLimitMin);
                while (!CheckHomePos()) Application.DoEvents();
                StopAxis(AxisNo, AxisStopHard);
                //MotorSetOff(AxisNo); // TODO 关电机？
            }
        }

        public void CheckMotorIsOnDlg() // 检查电机是否运行（弹窗）
        {
            if (!MotorEnIsOn)
            {
                DialogResult Res1 = MessageBox.Show("检测到电机未运行,是否启动电机", "Warning", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (Res1 == DialogResult.Yes)
                {
                    MotorSetOn(AxisNo);
                    return;
                }
                else
                {
                    return;
                }
            }
        }

        public double CheckVelLegitimacy(double checkVel) // 检查速度合法性
        {
            if (VelLimit)
            {
                if (checkVel < VelLimitMin)
                {
                    MessageBox.Show(string.Format("速度低于最小速度{0}pulse/ms,请重新输入", VelLimitMin.ToString()));
                    LnrVel_Tb.Text = VelLimitMin.ToString();
                    return VelLimitMin;
                }
                else if (checkVel > VelLimitMax)
                {
                    MessageBox.Show(string.Format("速度大于最大速度{0}pulse/ms,请重新输入", VelLimitMax.ToString()));
                    LnrVel_Tb.Text = VelLimitMax.ToString();
                    return VelLimitMax;
                }
                else
                {
                    return checkVel;
                }
            }
            else
            {
                return checkVel;
            }
        }

        public double CheckSmTLegitimacy(double checkSmt) // 检查平滑系数合法性
        {
            if (checkSmt < 0)
            {
                MessageBox.Show("平滑系数不合法，应在区间[0,1)内");
                LnrSmCoef_Tb.Text = "0";
                return 0.0;
            }
            else if (checkSmt >= 1)
            {
                MessageBox.Show("平滑系数不合法，应在区间[0,1)内");
                LnrSmCoef_Tb.Text = "0.999";
                return 0.999;
            }
            else
            {
                return checkSmt;
            }
        }

        public double CheckPosLegitimacy(double checkPos) // 检查位置合法性
        {
            return checkPos;
        }

        /***********************************************************日志记录*****************************************/

        private delegate void AlDlgate(string msg); // ActLog 的委托，防止跨线程更新UI
        private delegate void LclDlgate(); // LogClear 的委托，防止跨线程更新UI

        public void ActRecord(string msg) // 记录日志
        {
            AlDlgate ald = new AlDlgate(ActLogTb.AppendText);
            _cTime = DateTime.Now.ToLongTimeString().ToString();
            msg = _cTime + ": " + msg + "\n";
            this.Invoke(ald, new object[] { msg });
        }

        public void LogClear() // 清除日志
        {
            LclDlgate lcld = new LclDlgate(ActLogTb.Clear);
            this.Invoke(lcld);
        }

        /**↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓按钮事件↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓↓**/

        private void Ctrl_Btn_Click(object sender, EventArgs e) // 控制器状态按钮
        {
            short sRtn = 10;
            
            if (CtrlIsOn) // 控制板已开启
            {
                // 处理动作-> 关闭控制器
                // TODO 关闭控制板前要判断是否电机在运动
                if (MotorIsMoving) StopAxis(AxisNo, AxisStopSmooth);
                if (MotorEnIsOn) MotorSetOff(AxisNo);

                sRtn = CtrlClose();
                if (sRtn == 0)
                {
                    // 处理状态变量
                    CtrlIsOn = false;
                    // 处理UI
                    Ctrl_status.BackColor = Color.Red;
                    Ctrl_Btn.Text = "开启控制器";
                    UI_Reset();
                }
                
                UI_Reset(); // TODO: Debug时加的，注意删去

            }
            else
            {
                // 处理动作-> 开启控制器, 初始化
                sRtn = CtrlOpen();
                PreInitAfterCtrlOn();
                if (sRtn == 0)
                {
                    // 处理状态变量
                    CtrlIsOn = true;
                    // 处理UI
                    Ctrl_status.BackColor = Color.Lime;
                    Ctrl_Btn.Text = "关闭控制器";
                    UI_CtrlOn();
                }

                UI_CtrlOn(); // TODO: Debug时加的，注意删去
            }
        }

        private void Motor_Btn_Click(object sender, EventArgs e) // 电机使能按钮
        {
            short sRtn = 10;
            if (MotorEnIsOn) // 电机使能已开启
            {
                // 处理动作-> 关闭电机使能
                sRtn = MotorSetOff(AxisNo);
                if (sRtn == 0)
                {
                    // 处理状态变量
                    MotorEnIsOn = false;
                    // 处理UI
                    Motor_status.BackColor = Color.WhiteSmoke;
                    Motor_Btn.Text = "开启电机使能";
                }

            }
            else // 电机使能已关闭
            {
                // 处理动作-> 开启电机使能
                sRtn = MotorSetOn(AxisNo);
                if (sRtn == 0)
                {
                    // 处理状态变量
                    MotorEnIsOn = true;
                    // 处理UI
                    Motor_status.BackColor = Color.Lime;
                    Motor_Btn.Text = "关闭电机使能";
                }

            }
        }


        private void Driver_Btn_Click(object sender, EventArgs e) // 驱动器报警使能按钮
        {
            int sRtn = 10;
            if (DriverAlarmEn)
            {
                // 处理事件 -> 关闭驱动报警器使能
                sRtn = DriverAlarmEnOff(AxisNo);
                if (sRtn == 0)
                {
                    // 处理状态变量
                    DriverAlarmEn = false;
                    // 处理UI
                    Driver_Status.BackColor = Color.WhiteSmoke;
                }
            }
            else
            {
                // 处理事件 ->打开驱动器警报使能
                sRtn = DriverAlarmEnOn(AxisNo);
                if (sRtn == 0)
                {
                    // 处理状态变量
                    DriverAlarmEn = true;
                    // 处理UI
                    Driver_Status.BackColor = Color.Lime;
                }
            }
        }

        private void MaxLmtEn_Btn_Click(object sender, EventArgs e) // 正限位报警使能按钮
        {
            int sRtn = 10;
            if (MaxLmtAlarmEn)
            {
                // 处理事件 -> 关闭正限位报警使能
                sRtn = MaxLmtEnOff(AxisNo);
                if (sRtn == 0)
                {
                    // 处理状态变量
                    MaxLmtAlarmEn = false;
                    // 处理UI
                    MaxLmt_Status.BackColor = Color.WhiteSmoke;
                }
            }
            else
            {
                // 处理事件 -> 打开正限位报警使能
                sRtn = MaxLmtEnOn(AxisNo);
                if (sRtn == 0)
                {
                    // 处理状态变量
                    MaxLmtAlarmEn = true;
                    // 处理UI
                    MaxLmt_Status.BackColor = Color.Lime;
                }
            }
        }

        private void MinLmtEn_Btn_Click(object sender, EventArgs e) // 负限位报警使能按钮
        {
            int sRtn = 10;
            if (MinLmtAlarmEn)
            {
                // 处理事件 -> 关闭负限位报警使能
                sRtn = MinLmtEnOff(AxisNo);
                if (sRtn == 0)
                {
                    // 处理状态变量
                    MinLmtAlarmEn = false;
                    // 处理UI
                    MinLmt_Status.BackColor = Color.WhiteSmoke;
                }
            }
            else
            {
                // 处理事件 -> 打开负限位报警使能
                sRtn = MinLmtEnOn(AxisNo);
                if (sRtn == 0)
                {
                    // 处理状态变量
                    MinLmtAlarmEn = true;
                    // 处理UI
                    MinLmt_Status.BackColor = Color.Lime;
                }
            }
        }

        private void LogClear_Btn_Click(object sender, EventArgs e)// 清除日志按钮
        {
            LogClear();
        }

        private void SysReset_Btn_Click(object sender, EventArgs e) // 系统复位按钮
        {
            // 重置UI
            UI_Reset();
            // 初始化变量
            // TODO: 初始化变量
        }

        private void SensorSelect_Cbb_SelectedIndexChanged(object sender, EventArgs e) // 改变传感器下拉框
        {
            CurrEncoder = SensorSelect_Cbb.SelectedIndex;
        }

        private void UnitSelect_Cbb_SelectedIndexChanged(object sender, EventArgs e) // 改变全局单位下拉框
        {
            Unit = UnitSelect_Cbb.SelectedIndex;
            //if (Unit == 0)
            //{
            //    UI_pulse_cvt_mm();
            //}
            //else if(Unit == 1)
            //{
            //    UI_mm_cvt_pulse();
            //}

        }

        private void MotionPtn_Cbb_SelectedIndexChanged(object sender, EventArgs e) // 改变运动模式下拉框
        {
            // TODO 简单完成 细节待推敲
            if (MotionPtn_Cbb.SelectedIndex == 0)
            {
                LinearMotion_Gbx.Enabled = true;
                CircMotion_Gbx.Enabled = false;
            }

            if (MotionPtn_Cbb.SelectedIndex == 1)
            {
                LinearMotion_Gbx.Enabled = false;
                CircMotion_Gbx.Enabled = true;
            }
        }

        private void FindZero_Btn_Click(object sender, EventArgs e) // 寻零点按钮
        {
            
            JogModeAct();
            GoFindHome();
            //Thread.Sleep(500);
            ActRecord("FindZero Ok!");
            Set2ZeroPoint(AxisNo);
            ZeroPos_Lb.Text = (HomePos / 1000).ToString("0.0");
            if (ModeActivated == 0)
            {
                JogModeAct();
            }

            if (ModeActivated == 1)
            {
                TrapModeAct();
            }
        }

        private void SetCrtPos2Zero_Btn_Click(object sender, EventArgs e) // 定义零点按钮
        {

        }

        private void LnrVel_Tb_TextChanged(object sender, EventArgs e) // 速度输入框值改变
        {
            SettingChanged = true;
            LnrParmSet_Btm.BackColor = Color.Tomato;
        }

        private void LnrAcc_Tb_TextChanged(object sender, EventArgs e) // 加速度输入框值改变
        {
            SettingChanged = true;
            LnrParmSet_Btm.BackColor = Color.Tomato;
        }

        private void LnrDec_Tb_TextChanged(object sender, EventArgs e) // 减速度输入框值改变
        {
            SettingChanged = true;
            LnrParmSet_Btm.BackColor = Color.Tomato;
        }

        private void LnrSmT_Tb_TextChanged(object sender, EventArgs e) // 平滑时间输入框值改变
        {
            SettingChanged = true;
            LnrParmSet_Btm.BackColor = Color.Tomato;
        }

        private void LnrParmSet_Btm_Click(object sender, EventArgs e) // 设置按钮
        {
            double LnrVel = double.Parse(LnrVel_Tb.Text);
            double LnrAcc = double.Parse(LnrAcc_Tb.Text);
            double LnrDec = double.Parse(LnrDec_Tb.Text);
            double LnrSmCoef = double.Parse(LnrSmCoef_Tb.Text);

            LnrVel = CheckVelLegitimacy(LnrVel); // 检查速度合法性
            LnrSmCoef = CheckSmTLegitimacy(LnrSmCoef); // 检查平滑系数合法性
  

            if (ModeActivated == 0) // Jog模式激活，更新TJog结构体参数
            {
                HandyVel = LnrVel;
                TJog.acc = LnrAcc;
                TJog.dec = LnrDec;
                TJog.smooth = LnrSmCoef;

                SetVel(AxisNo, HandyVel);
                SetParmOfJog(AxisNo, TJog);
            }

            if (ModeActivated == 1) // Trap模式激活，更新TTrap结构体参数
            {
                HandyVel = LnrVel;
                TTrap.acc = LnrAcc;
                TTrap.dec = LnrDec;
                TTrap.smoothTime = (short) (LnrSmCoef * 50);

                SetVel(AxisNo, HandyVel);
                SetParmOfTrap(AxisNo, TTrap);
            }

            SettingChanged = false;
            LnrParmSet_Btm.BackColor = Color.LightGreen;
        }


        private void MoveForward_Btn_MouseDown(object sender, MouseEventArgs e) // 手动正向 按下
        {
            HandyFowardMouseDown = true;
            HandyGoForward(HandyVel);
        }

        private void MoveForward_Btn_MouseUp(object sender, MouseEventArgs e) // 手动正向 松开
        {
            HandyFowardMouseDown = false;
            StopAxis(AxisNo, AxisStopHard);
            MouseMovement = false;
        }

        private void MoveBackward_Btn_MouseDown(object sender, MouseEventArgs e) // 手动负向 按下
        {
            HandyBackMouseDown = true;
            HandyGoBackward(HandyVel);
        }

        private void MoveBackward_Btn_MouseUp(object sender, MouseEventArgs e) // 手动负向 松开
        {
            HandyBackMouseDown = false;
            StopAxis(AxisNo, AxisStopHard);
            MouseMovement = false;
        }


        private void LnrHandyMd_Rdb_CheckedChanged(object sender, EventArgs e) // 手动模式 选中
        {
            TJogUpdateFromUI(); // 更新一下参数
            JogModeAct();
            ModeActivated = 0; // JogMode
            HandyMode_Gbx.Enabled = true;
            AutoMode_Gbx.Enabled = false;
        }

        private void LnrAutoMd_Rdb_CheckedChanged(object sender, EventArgs e) // 自动模式 选中
        {
            TTrapUpdateFromUI(); // 更新一下参数
            TrapModeAct();
            ModeActivated = 1; // TrapMode
            HandyMode_Gbx.Enabled = false;
            AutoMode_Gbx.Enabled = true;
        }

        private void LnrIncrePos_Rdb_CheckedChanged(object sender, EventArgs e) // 自动模式->相对位置 选中
        {
            IncreModeActivated = true;
            LnrIncrePos_Tb.Enabled = true;
            LnrAbsPos_Tb.Enabled = false;
        }

        private void LnrAbsPos_Rdb_CheckedChanged(object sender, EventArgs e) // 自动模式->绝对位置 选中
        {
            IncreModeActivated = false;
            LnrIncrePos_Tb.Enabled = false;
            LnrAbsPos_Tb.Enabled = true;
        }


        private void Back2Zero_Btn_Click(object sender, EventArgs e) // 回零点按钮
        {
            CheckMotorIsOnDlg();
            SetPos(AxisNo, 0);
            CommandUpdate(AxisNo);
        }

        private void LnrIncrePos_Tb_Leave(object sender, EventArgs e) // 增量位置，焦点离开
        {
            AutoAimIncre = double.Parse(LnrIncrePos_Tb.Text) * 1000;
        }

        private void LnrAbsPos_Tb_Leave(object sender, EventArgs e) // 绝对位置，焦点离开
        {
            AutoAimAbs = double.Parse(LnrAbsPos_Tb.Text) * 1000;
        }

        private void LnrStart_Btn_Click(object sender, EventArgs e) // 启动按钮
        {
            int pulse = 0;
            CheckMotorIsOnDlg();
            GetPos(AxisNo, out PulsePos);
            if (IncreModeActivated) // 增量模式
            {
                pulse = (int) (PulsePos  + AutoAimIncre);
            }
            else // 绝对模式
            {
                pulse = (int) (AutoAimAbs);
            }
            SetPos(AxisNo,pulse);
            CommandUpdate(AxisNo);
        }

        private void About_Btn_Click(object sender, EventArgs e) // 关于按钮
        {
            SetCrdPrm();
            CrdFifoClear();
            LnXY(line1);
            CrdStart();
            Crd_Tmr.Enabled = true;

        }

        private void GetStatus_Tmr_Tick(object sender, EventArgs e) // 获取状态定时器任务
        {
            ClearStatusAxis(AxisNo);
            GetStatusAxis(AxisNo);
            PrfPos = GetPrfModeOfAxis(AxisNo);
            if (CurrEncoder != -1) GetPosFromSensor(AxisNo);
            AbsPos_Lb.Text = (AbsPos / 1000).ToString("0.0");
            RelPos_Lb.Text = (RelPos / 1000).ToString("0.0");
 
            CurrVel = Math.Abs(AbsPos - AbsPos0) / GetStatus_Tmr.Interval;
            CurrVel_Lb.Text = (CurrVel).ToString("0.000");
        }

        private void HandyMode_Tmr_Tick(object sender, EventArgs e) // 手动模式的定时器，轮询按键状态
        {
            if (!HandyFowardMouseDown && !HandyBackMouseDown) // 没有任何键按下
            {
                if (MouseMovement)
                {
                    MouseMovement = false;
                    StopAxis(AxisNo, AxisStopHard);
                }
                return;
            }
            if (!MouseMovement)
            {
                if (HandyFowardMouseDown) { MouseMovement = true; HandyGoForward(HandyVel); }
                if (HandyBackMouseDown) { MouseMovement = true; HandyGoBackward(HandyVel); }
            }
        }

        private void Crd_Tmr_Tick(object sender, EventArgs e)
        {
            GetCrdStatus(out CrdRun, out CrdSegment);
            ActRecord("Run " + CrdRun.ToString() + ", " + "Segment " + CrdSegment.ToString());
        }

        private void MotorCtrlForm_Load(object sender, EventArgs e) // 加载窗口时
        {
            Init();
            UI_Init();
        }

        private void MotorCtrlForm_FormClosed(object sender, FormClosedEventArgs e) // 关闭窗口时，收尾工作
        {
            if (MotorIsMoving) StopAxis(AxisNo, AxisStopHard);
            if (MotorEnIsOn) MotorSetOff(AxisNo);
            CtrlClose();
        }



        public MotorCtrlForm()
        {
            InitializeComponent();
        }


    }
}
