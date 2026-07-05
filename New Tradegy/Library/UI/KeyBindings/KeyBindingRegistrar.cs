using System.Windows.Forms;
using New_Tradegy.Library.Models;
using New_Tradegy.Library.UI.KeyBindings;

namespace New_Tradegy.Library.UI.KeyBindings
{
    public static class KeyBindingRegistrar
    {
        // Function
        // Number
        // Top
        // Home
        // Bottom
        // Control
        public static void RegisterAll()
        {
            // private static readonly Dictionary<(Keys key, bool shift, bool ctrl, bool alt), Action<Form>>

            // Number
            KeyBindingManager.Register('1', false, false, ActionHandlers.AddTopRankToInterestedOnlyList);

            KeyBindingManager.Register('2', false, false, ActionHandlers.AddSecondRankToInterestedOnlyList);
            
            KeyBindingManager.Register('3', true, false, ActionHandlers.ClearInterestedOnlyList);

            // Function 
            if (!g.test)
            {

                // F2 : F9
                KeyBindingManager.Register(Keys.A, false, false, false, ActionHandlers.DealDoubleKey);
                KeyBindingManager.Register(Keys.A, true, false, false, ActionHandlers.DealHalf);
                KeyBindingManager.Register(Keys.A, false, true, false, ActionHandlers.DealCancel);



                KeyBindingManager.Register('q', false, false, ActionHandlers.TimeOneForwardsKey);


                // F1 : F5
                //KeyBindingManager.Register(Keys.F11, false, false, false, ActionHandlers.ConfirmSellToggle);                   // F4 : F11

                //KeyBindingManager.Register(Keys.F11, false, false, false, ActionHandlers.ConfirmSellToggle);           // F4 : F11

                KeyBindingManager.Register('m', false, false, ActionHandlers.SaveAllStocksAsync);

            }
            else
            {
                KeyBindingManager.Register(Keys.A, false, false, false, ActionHandlers.TimeOneForwardsKey);
                KeyBindingManager.Register(Keys.A, true, false, false, ActionHandlers.TimeOneBackwardsKey);
                KeyBindingManager.Register(Keys.A, false, true, false, ActionHandlers.TimeShortMoveKey);

                KeyBindingManager.Register('q', false, false, ActionHandlers.TimeOneForwardsKey);
                KeyBindingManager.Register('Q', false, false, ActionHandlers.TimeOneBackwardsKey);
                KeyBindingManager.Register('w', false, false, ActionHandlers.TimeShortMoveKey);
                KeyBindingManager.Register('W', false, false, ActionHandlers.TimeLongMoveKey);

                KeyBindingManager.Register('e', false, false, ActionHandlers.TimeTenForwardsKey);
                KeyBindingManager.Register('E', false, false, ActionHandlers.TimeTenBackwardsKey);
                KeyBindingManager.Register('r', false, false, ActionHandlers.TimeThirtyForwardsKey);
                KeyBindingManager.Register('R', false, false, ActionHandlers.TimeThirtyBackwardsKey);

            }

            KeyBindingManager.Register(Keys.S, false, false, false, () => ActionHandlers.SetMode(DisplayMode.피분));

            //if (g.test)
            //    KeyBindingManager.Register(Keys.S, false, false, false, ActionHandlers.TimeShortMoveKey); //?

            KeyBindingManager.Register(Keys.S, true, false, false, () => ActionHandlers.SetMode(DisplayMode.푀누));
            KeyBindingManager.Register(Keys.S, false, true, false, () => ActionHandlers.SetMode(DisplayMode.종누));

            KeyBindingManager.Register(Keys.D, false, false, false, () => ActionHandlers.SetMode(DisplayMode.배차));
            KeyBindingManager.Register(Keys.D, true, false, false, () => ActionHandlers.SetMode(DisplayMode.피올));
            KeyBindingManager.Register(Keys.D, false, true, false, () => ActionHandlers.SetMode(DisplayMode.닥올));

            KeyBindingManager.Register(Keys.Z, false, false, false, () => ActionHandlers.SetMode(DisplayMode.등합));
            KeyBindingManager.Register(Keys.Z, true, false, false, () => ActionHandlers.SetMode(DisplayMode.상순));
            KeyBindingManager.Register(Keys.Z, false, true, false, () => ActionHandlers.SetMode(DisplayMode.저순));

            KeyBindingManager.Register(Keys.X, false, false, false, () => ActionHandlers.SetMode(DisplayMode.분거));
            KeyBindingManager.Register(Keys.X, true, false, false, () => ActionHandlers.SetMode(DisplayMode.평균));
            KeyBindingManager.Register(Keys.X, false, true, false, () => ActionHandlers.SetMode(DisplayMode.편차));

            KeyBindingManager.Register(Keys.C, false, false, false, () => ActionHandlers.SetMode(DisplayMode.섹터));
            KeyBindingManager.Register(Keys.C, true, false, false, () => ActionHandlers.SetMode(DisplayMode.s10p));
            KeyBindingManager.Register(Keys.C, false, true, false, () => ActionHandlers.SetMode(DisplayMode.s10m));




            //KeyBindingManager.Register('d', false, false, ActionHandlers.SectorDraw);

            KeyBindingManager.Register('T', false, false, ActionHandlers.ShrinkOrNotTenPlusKey);
            KeyBindingManager.Register('t', false, false, ActionHandlers.ShrinkOrNotTenMinusKey);

            KeyBindingManager.Register('n', false, false, ActionHandlers.NextPage);
            KeyBindingManager.Register('n', true, false, ActionHandlers.PreviousPage);

            KeyBindingManager.Register('o', false, false, ActionHandlers.OpenFilesKey);
            KeyBindingManager.Register('O', false, false, ActionHandlers.OpenMemoKey); // not implemented
            KeyBindingManager.Register('p', false, false, ActionHandlers.NewsPeoridKey);
            KeyBindingManager.Register('[', false, false, ActionHandlers.DrawBollingerKey);
            KeyBindingManager.Register(']', false, false, ActionHandlers.DrawForeignAndInstituteKey);
            KeyBindingManager.Register('\\', false, false, ActionHandlers.DrawNormaStockKey);

            //KeyBindingManager.Register('j', false, false, ActionHandlers.nRowDecrease);
            //KeyBindingManager.Register('J', false, false, ActionHandlers.nRowIncrease);
            //KeyBindingManager.Register('k', false, false, ActionHandlers.nColDecrease);
            //KeyBindingManager.Register('K', false, false, ActionHandlers.nColIncrease);

            // Top

            //KeyBindingManager.Register('w', true, false, ActionHandlers.WeightControlKey);

            // KeyBindingManager.Register('t', false, false, ActionHandlers.ToggleChart1Focus);
            //KeyBindingManager.Register('i', false, false, ActionHandlers.IncreasePassControlPercentage);
            // KeyBindingManager.Register('I', false, false, ActionHandlers.DecreasePassControlPercentage);



            // Home
            //KeyBindingManager.Register('a', false, false, ActionHandlers.ActiveToInterestedKey);

            //KeyBindingManager.Register('s', false, true, ActionHandlers.AddInterestToggle);






            // Bottom


            
            //KeyBindingManager.Register('X', false, false, ActionHandlers.RemoveInterestedWithBidListKey);
            //KeyBindingManager.Register('c', false, false, ActionHandlers.KillWebTxtFormKey);

            //KeyBindingManager.Register(Keys.Oemcomma, false, false, false, ActionHandlers.PrevDatewisePage);  // '<'
            //KeyBindingManager.Register(Keys.OemPeriod, false, false, false, ActionHandlers.NextDatewisePage); // '>'


            //KeyBindingManager.Register('c', true, false, ActionHandlers.이평_증순);



            //KeyBindingManager.Register(' ', false, false, ActionHandlers.ActiveClearKey);
            KeyBindingManager.Register(',', false, false, ActionHandlers.GoPrevDay); // , 로 이전
            KeyBindingManager.Register('.', false, false, ActionHandlers.GoNextDay); // . 로 다음
            //KeyBindingManager.Register('=', false, false, ActionHandlers.GoBackToOrigin); // = 로 복귀

            //// 키 등록
            //KeyBindingRegistrar.Register('<', () => DateNavigator.GoPrevDay());
            //KeyBindingRegistrar.Register('>', () => DateNavigator.GoNextDay());
            //KeyBindingRegistrar.Register('=', () => DateNavigator.GoBackToOrigin()); // 복귀

            //// 프로그램 초기화 시
            //DateNavigator.AfterLoad = () =>
            //{
            //    // 차트/표/패널 등 화면 갱신 루틴
            //    UpdateViews();
            //};
        }
    }
}
