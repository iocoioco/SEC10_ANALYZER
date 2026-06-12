// Form1.PlanGrid.cs  (붙여넣기/덮어쓰기용)
// ------------------------------------------------------------
using New_Tradegy.Library;
using New_Tradegy.Library.Deals;
using New_Tradegy.Library.Utils;
using System;
using System.Drawing;
using System.Windows.Forms;
using static New_Tradegy.Library.Listeners.BookBidGeneratorStock;

namespace New_Tradegy
{
    public partial class Form1 : Form
    {
        // ------------------------------------------------------------
        // ✅ Grid Init
        // ------------------------------------------------------------
        private void InitPlanGrid()
        {
            // ─────────────────────────────────────────────
            // 기본 초기화
            // ─────────────────────────────────────────────
            dgvPlan.AutoGenerateColumns = false;
            dgvPlan.Columns.Clear();
            dgvPlan.Rows.Clear();

            // ─────────────────────────────────────────────
            // 스크롤 / 리사이즈 방지 (고정 그리드)
            // ─────────────────────────────────────────────
            dgvPlan.ScrollBars = ScrollBars.None;
            dgvPlan.AllowUserToResizeColumns = false;
            dgvPlan.AllowUserToResizeRows = false;

            // ─────────────────────────────────────────────
            // 헤더 / 선택 / 행 추가·삭제
            // ─────────────────────────────────────────────
            dgvPlan.RowHeadersVisible = false;
            dgvPlan.ColumnHeadersVisible = true;   // 필요 없으면 false
            dgvPlan.AllowUserToAddRows = false;
            dgvPlan.AllowUserToDeleteRows = false;

            dgvPlan.MultiSelect = false;
            dgvPlan.SelectionMode = DataGridViewSelectionMode.FullRowSelect;

            // ─────────────────────────────────────────────
            // 편집 정책 (Programmatic Only)
            // ─────────────────────────────────────────────
            dgvPlan.EditMode = DataGridViewEditMode.EditProgrammatically;

            // ─────────────────────────────────────────────
            // 레이아웃: 행 높이 (화면 비례)
            // 호가창 = H/3, 그 안에 11행 배치
            // ─────────────────────────────────────────────
            int planRowHeight = g.cellHeight;
            dgvPlan.RowTemplate.Height = planRowHeight;

            // ─────────────────────────────────────────────
            // 폰트
            // ─────────────────────────────────────────────
            var family = dgvPlan.Font.FontFamily;
            var gridFont = new Font(family, 8.5f, FontStyle.Regular);

            dgvPlan.DefaultCellStyle.Font = gridFont;
            dgvPlan.ColumnHeadersDefaultCellStyle.Font = gridFont;

            // === 컬럼 정의 ===
            var colOn = new DataGridViewCheckBoxColumn
            {
                Name = "OnColumn",
                HeaderText = " ",
                ThreeState = false
            };

            var colC1 = new DataGridViewTextBoxColumn
            {
                Name = "Col1",
                HeaderText = "잔량",
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleLeft }
            };

            var colC2 = new DataGridViewTextBoxColumn
            {
                Name = "Col2",
                HeaderText = "가변",
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleCenter }
            };

            var colC3 = new DataGridViewTextBoxColumn
            {
                Name = "Col3",
                HeaderText = "푀돈",
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            var colC4 = new DataGridViewTextBoxColumn
            {
                Name = "Col4",
                HeaderText = "배차",
                DefaultCellStyle = { Alignment = DataGridViewContentAlignment.MiddleRight }
            };

            dgvPlan.AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None;
            dgvPlan.Columns.AddRange(colOn, colC1, colC2, colC3, colC4);

            // ===== 위치/사이즈: 10x3 그리드에서 (2,1), w=1/10, h=1/3 =====
            int W = g.ChartManager.Chart1.Width / 10;
            int H = g.ChartManager.Chart1.Height / 3;

            int x = 3 * W;
            int y = H - 10; // 상단 호가창 아래 시작 위치

            dgvPlan.Location = new Point(x, y);
            dgvPlan.Size = new Size(W, H + 2);

            AdjustPlanColumnWidths();

            RefreshPlanGrid();

            // === 아래쪽 빈 줄의 ON 체크 표시 안 보이게 처리 ===
            dgvPlan.CellPainting -= dgvPlan_CellPainting_HideEmptyOn;
            dgvPlan.CellPainting += dgvPlan_CellPainting_HideEmptyOn;

            // 이벤트
            dgvPlan.CellMouseClick -= dgvPlan_CellMouseClick;
            dgvPlan.CellMouseClick += dgvPlan_CellMouseClick;

            dgvPlan.CellEndEdit -= dgvPlan_CellEndEdit;
            dgvPlan.CellEndEdit += dgvPlan_CellEndEdit;
        }

        // ------------------------------------------------------------
        // ✅ Refresh: Plans -> Grid  (2줄 구조)
        // ------------------------------------------------------------
        private void RefreshPlanGrid()
        {
            var plans = g.TradePlanManager.Plans;

            dgvPlan.Rows.Clear();

            for (int i = 0; i < plans.Count; i++)
            {
                var p = plans[i];

                int mainRow = dgvPlan.Rows.Add(); // 위줄
                int subRow = dgvPlan.Rows.Add();  // 아래줄

                // --- 위줄: 종목 / 매매 / 가격 / 금액(만) ---
                dgvPlan["OnColumn", mainRow].Value = p.On;
                dgvPlan["Col1", mainRow].Value = p.Symbol;      // 종목
                dgvPlan["Col2", mainRow].Value = ToSideBS(p.Side);      // 매매
                dgvPlan["Col3", mainRow].Value = p.BasePrice;   // 가격
                dgvPlan["Col4", mainRow].Value = p.AmountK;     // 금액(만)

                // --- 아래줄: 잔량 / 가변 / 푀돈 / 배차 ---
                dgvPlan["OnColumn", subRow].Value = p.On;
                dgvPlan["Col1", subRow].Value = p.Quantity;     // 잔량(수량)
                dgvPlan["Col2", subRow].Value = p.TriggerPctX100;  // 가변(오프셋)
                dgvPlan["Col3", subRow].Value = p.Foeton;       // 푀돈
                dgvPlan["Col4", subRow].Value = p.Multiplier;   // 배차
            }
        }
        private static string ToSideBS(string side)
        {
            if (string.IsNullOrWhiteSpace(side)) return "";
            side = side.Trim();

            // 이미 B/S면 그대로
            if (side.Equals("B", StringComparison.OrdinalIgnoreCase)) return "B";
            if (side.Equals("S", StringComparison.OrdinalIgnoreCase)) return "S";

            // 한글
            if (side == "매수") return "B";
            if (side == "매도") return "S";

            // 영문(혹시 들어오면)
            if (side.Equals("Buy", StringComparison.OrdinalIgnoreCase)) return "B";
            if (side.Equals("Sell", StringComparison.OrdinalIgnoreCase)) return "S";

            return side; // 알 수 없으면 원본 표시
        }


        // ------------------------------------------------------------
        // ✅ rowIndex -> PlanLine (2줄 매핑)
        // ------------------------------------------------------------
        private TradePlanLine GetPlanLine(int rowIndex)
        {
            if (rowIndex < 0) return null;

            int lineIndex = rowIndex / 2;
            var plans = g.TradePlanManager.Plans;

            if (lineIndex < 0 || lineIndex >= plans.Count)
                return null;

            return plans[lineIndex];
        }

        // ------------------------------------------------------------
        // ✅ 번호 재부여 (DataBoundItem 쓰면 안 됨!)
        // ------------------------------------------------------------
        private void RenumberAllPlans()
        {
            int no = 1;

            foreach (DataGridViewRow row in dgvPlan.Rows)
            {
                if (row.IsNewRow) continue;

                // 짝수행 = 위줄(종목줄)
                bool isUpperRow = (row.Index % 2 == 0);
                if (!isUpperRow) continue;

                var line = GetPlanLine(row.Index);   // ✅ 핵심: DataBoundItem 쓰지 말고 이걸로
                if (line == null) continue;

                line.DisplayNo = no++;
            }

            dgvPlan.Refresh();
        }
        public void RefreshPlanUi()
        {
            RefreshPlanGrid();
            RenumberAllPlans();
        }

        // ------------------------------------------------------------
        // ✅ CellEndEdit: 1/4 버전(옛 컬럼명) → 현재 컬럼명(Col1~Col4)로 수정
        // 위줄(짝수): Col1=Symbol, Col2=Side, Col3=BasePrice, Col4=AmountK
        // 아래줄(홀수): Col1=Quantity, Col2=OffsetTicks, Col3=Foeton, Col4=Multiplier
        // ------------------------------------------------------------
        private void dgvPlan_CellEndEdit(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var line = GetPlanLine(e.RowIndex);
            if (line == null) return;

            string colName = dgvPlan.Columns[e.ColumnIndex].Name;
            var cell = dgvPlan.Rows[e.RowIndex].Cells[e.ColumnIndex];
            string text = Convert.ToString(cell.Value)?.Trim();

            // OnColumn은 체크박스라 EndEdit로 다루지 말자 (CellMouseClick에서 토글)
            if (colName == "OnColumn") return;

            if (string.IsNullOrWhiteSpace(text)) return;

            bool isUpperRow = (e.RowIndex % 2 == 0); // 짝수행=위줄

            if (isUpperRow)
            {
                switch (colName)
                {
                    case "Col1": // Symbol
                        line.Symbol = text;
                        break;

                    case "Col2": // Side
                        {
                            string t = text.Trim();

                            if (t.Equals("B", StringComparison.OrdinalIgnoreCase) || t == "매수" || t.Equals("Buy", StringComparison.OrdinalIgnoreCase))
                                line.Side = "매수";
                            else if (t.Equals("S", StringComparison.OrdinalIgnoreCase) || t == "매도" || t.Equals("Sell", StringComparison.OrdinalIgnoreCase))
                                line.Side = "매도";

                            // ✅ 표시도 즉시 B/S로 다시 덮기 (안 하면 '매수' 글자가 남아있을 수 있음)
                            dgvPlan["Col2", e.RowIndex].Value = ToSideBS(line.Side);
                            break;
                        }



                    case "Col3": // BasePrice
                        if (int.TryParse(text, out int price) && price >= 0)
                        {
                            line.BasePrice = price;

                            // ✅ Quantity(잔량 임계치)는 수기 입력값이므로 자동 계산/자동 반영 제거
                            // (아래줄 Col1은 건드리지 않는다)
                        }
                        break;

                    case "Col4": // AmountK
                        if (int.TryParse(text, out int amtK) && amtK >= 0)
                        {
                            line.AmountK = amtK;

                            // ✅ Quantity(잔량 임계치)는 수기 입력값이므로 자동 계산/자동 반영 제거
                            // (아래줄 Col1은 건드리지 않는다)
                        }
                        break;
                }
            }
            else
            {
                switch (colName)
                {
                    case "Col1": // Quantity
                        if (int.TryParse(text, out int qty) && qty >= 0)
                            line.Quantity = qty;
                        break;

                    case "Col2": // TriggerPctX100
                        if (int.TryParse(text, out int pct) && pct >= 0)
                            line.TriggerPctX100 = pct;
                        break;


                    case "Col3": // Foeton
                        if (double.TryParse(text, out double f))
                            line.Foeton = f;
                        break;

                    case "Col4": // Multiplier
                        if (double.TryParse(text, out double m))
                            line.Multiplier = m;
                        break;
                }
            }

            line.LastModified = DateTime.Now;

            // 화면 갱신
            dgvPlan.Refresh();
        }


        // ------------------------------------------------------------
        // ✅ 1/2일 버전 CellMouseClick 기반으로 수정 (DataBoundItem 제거 + tick 안전화)
        // 네 규칙:
        // - 위줄 Col1 클릭 = 삭제
        // - 아래줄 Col1/Col3/Col4 = 직접 숫자 입력 편집
        // - Left=+1, Right=-1
        // - OnColumn 토글은 위줄만
        // - Col2(아래줄) OffsetTicks +/-1
        // - Col3(위줄) BasePrice 1틱 이동
        // - Col4(위줄) AmountK 단계 이동
        // ------------------------------------------------------------
        private void dgvPlan_CellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var grid = (DataGridView)sender;
            string colName = grid.Columns[e.ColumnIndex].Name;

            bool isUpperRow = (e.RowIndex % 2 == 0);
            bool isLowerRow = !isUpperRow;

            // 0) 종목 클릭 시 플랜 삭제 (위줄 Col1)
            if (isUpperRow && colName == "Col1")
            {
                TradePlanLine planDel = GetPlanLine(e.RowIndex);
                if (planDel != null)
                {
                    g.TradePlanManager.Plans.Remove(planDel);

                    // 아래줄 먼저 제거
                    if (e.RowIndex + 1 < grid.Rows.Count)
                        grid.Rows.RemoveAt(e.RowIndex + 1);

                    // 위줄 제거
                    grid.Rows.RemoveAt(e.RowIndex);

                    // 번호 갱신
                    RenumberAllPlans();
                }
                return;
            }

            // 아래줄에서 직접 입력 편집 허용 (Col1/Col3/Col4)
            if (isLowerRow && (colName == "Col1" || colName == "Col3" || colName == "Col4"))
            {
                grid.CurrentCell = grid.Rows[e.RowIndex].Cells[e.ColumnIndex];
                grid.BeginEdit(true);
                return;
            }

            // 방향: 왼쪽=+1, 오른쪽=-1
            int dir = 0;
            if (e.Button == MouseButtons.Left) dir = 1;
            else if (e.Button == MouseButtons.Right) dir = -1;

            if (colName != "OnColumn" && dir == 0) return;

            TradePlanLine plan = GetPlanLine(e.RowIndex);
            if (plan == null) return;

            // 1) ON 토글 (위줄만)
            if (colName == "OnColumn")
            {
                if (!isUpperRow) return;

                if (e.Button == MouseButtons.Left)
                {
                    plan.On = !plan.On;
                    grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = plan.On;

                    // 아래줄 ON 동기화
                    if (e.RowIndex + 1 < grid.Rows.Count)
                        grid["OnColumn", e.RowIndex + 1].Value = plan.On;

                    RenumberAllPlans();
                }
                return;
            }

            // 2) 가변(Col2) 아래줄 ±1
            if (colName == "Col2" && isLowerRow)
            {
                int step = TradePlanManager.IsEtfOrLeveraged(plan.Symbol) ? 1 : 5;

                int value = plan.TriggerPctX100;
                value += dir * step;

                // 범위는 % 기준으로 재설정
                if (value < 1) value = 1;     // 0.01%
                if (value > 300) value = 300; // 3.00%


                plan.TriggerPctX100 = value;
                grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = value;
                return;
            }

            // 3) 가격(Col3, 위줄): BasePrice 1틱 조정
            if (colName == "Col3" && isUpperRow)
            {
                int basePx = plan.BasePrice > 0 ? plan.BasePrice : 0;
                if (basePx <= 0)
                {
                    // BasePrice가 0이면 일단 현재 셀 값으로 시도
                    int.TryParse(Convert.ToString(grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value), out basePx);
                }
                if (basePx <= 0) return;

                int tick = DealUtils.GetTick(plan.Symbol, basePx);
                int newPrice = basePx + dir * tick;

                plan.BasePrice = newPrice;
                grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = newPrice;

                plan.LastModified = DateTime.Now;
                return;
            }
            // 4) 금액(Col4, 위줄): AmountK 1.25배 / 0.8배
            if (colName == "Col4" && isUpperRow)
            {
                int cur = plan.AmountK;

                int next = (dir > 0)
                    ? IncreaseAmountK(cur)   // Left click
                    : DecreaseAmountK(cur);  // Right click

                plan.AmountK = next;
                grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value = next;

                // ✅ Quantity(잔량 임계치)는 수기 입력값이므로 자동 계산 제거
                plan.LastModified = DateTime.Now;
                return;
            }


            // 5) 그 외는 무시
        }
        private int IncreaseAmountK(int cur)
        {
            // 0에서 올릴 때는 기본 100만(원하면 50/200 등으로 바꿔)
            if (cur < 100) return 100;

            // 1.33배
            double next = cur * 1.33;

            // 만원단위 int라서 반올림
            int v = (int)Math.Round(next, MidpointRounding.AwayFromZero);

            return v;
        }

        private int DecreaseAmountK(int cur)
        {
            double next = cur * 0.66;
            int v = (int)Math.Round(next, MidpointRounding.AwayFromZero);

            if (v < 100) return 0;
            return v;
        }

        private void dgvPlan_CellPainting_HideEmptyOn(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0) return;

            var grid = (DataGridView)sender;
            string colName = grid.Columns[e.ColumnIndex].Name;

            bool isUpperRow = (e.RowIndex % 2 == 0);   // 짝수행 = 위줄(종목줄)
            bool isLowerRow = !isUpperRow;

            // 1) 아래줄의 OnColumn 체크박스 숨기기 (기존 기능)
            if (colName == "OnColumn" && isLowerRow)
            {
                // 네가 원래 쓰던 "체크박스 안 보이게" 코드가 여기에 있었을 거야.
                // 대략 이런 형태일 가능성이 큼:
                e.Handled = true;
                e.PaintBackground(e.ClipBounds, true);  // 바탕만 그리고 체크박스는 안 그림
                return;
            }

            // 2) 위줄 OnColumn 셀에 번호(장)를 그려 넣기 (신규 기능)
            if (colName == "OnColumn" && isUpperRow)
            {
                e.Handled = true;

                // 기본 배경 + 체크박스 먼저 그림
                e.PaintBackground(e.ClipBounds, true);
                e.PaintContent(e.ClipBounds);

                // DataBoundItem 에서 DisplayNo 읽어오기
                if (grid.Rows[e.RowIndex].DataBoundItem is TradePlanLine line &&
                    line.DisplayNo > 0)
                {
                    string text = line.DisplayNo.ToString();

                    // 체크박스 오른쪽에 작게 번호 표시
                    Rectangle rect = e.CellBounds;
                    rect.X += 14;   // 체크박스 폭만큼 오른쪽으로
                    rect.Width -= 16;

                    TextRenderer.DrawText(
                        e.Graphics,
                        text,
                        e.CellStyle.Font,
                        rect,
                        e.CellStyle.ForeColor,
                        TextFormatFlags.Right | TextFormatFlags.VerticalCenter);
                }

                return;
            }

            // 그 외 셀들은 기본 그리기
        }
        private void AdjustPlanColumnWidths()
        {
            if (dgvPlan.Columns.Count < 5) return;

            // 전체 폭 (RowHeadersVisible = false 이므로 그대로 사용)
            int total = dgvPlan.ClientSize.Width;
            if (total <= 0) return;

            // ── 고정 폭 설정 ──────────────────────
            int w0 = 22;  // ON 체크
            int w1 = 50;  // 잔량 5자리
            int w2 = 26;  // 가변 B/S + 숫자
            int w3 = 50;  // 푀돈 7자리

            int used = w0 + w1 + w2 + w3;

            // 남은 폭은 전부 배차(Col4)가 먹는다.
            int w4 = total - used;
            if (w4 < 20) w4 = 20;   // 너무 작으면 최소 20px

            // 만약 total 자체가 너무 좁아서 음수가 되면,
            // 어쩔 수 없이 비례로 조금 줄이자 (극단 상황 방지용)
            if (used + 20 > total)
            {
                double f = (double)total / (used + 20);
                w0 = (int)(w0 * f);
                w1 = (int)(w1 * f);
                w2 = (int)(w2 * f);
                w3 = (int)(w3 * f);
                w4 = total - (w0 + w1 + w2 + w3);
            }

            dgvPlan.Columns[0].Width = w0; // OnColumn
            dgvPlan.Columns[1].Width = w1; // 잔량
            dgvPlan.Columns[2].Width = w2; // 가변
            dgvPlan.Columns[3].Width = w3; // 푀돈
            dgvPlan.Columns[4].Width = w4; // 배차
        }


        public void SelectPlanRow(int planIndex)
        {
            // 2줄 구조니까 위줄 인덱스 계산
            int mainRow = planIndex * 2;

            if (dgvPlan.InvokeRequired)
            {
                dgvPlan.Invoke((MethodInvoker)(() => SelectPlanRow(planIndex)));
                return;
            }

            dgvPlan.ClearSelection();

            if (mainRow >= 0 && mainRow < dgvPlan.Rows.Count)
            {
                dgvPlan.Rows[mainRow].Selected = true;
                dgvPlan.CurrentCell = dgvPlan[0, mainRow];
            }
        }



        public void SetPlanColorActivePublic(TradePlanLine plan, bool on)
     => SetPlanColorActive(plan, on);

        public void SetPlanColorQueuedPublic(TradePlanLine plan, bool on)
            => SetPlanColorQueued(plan, on);


        // 팝업 실행 중인 라인 (진한 색)
        private void SetPlanColorActive(TradePlanLine plan, bool on)
        {
            int row = plan.DisplayNo * 2 - 2;   // 위줄 기준 (0-based)
            if (row < 0 || row >= dgvPlan.Rows.Count) return;

            var color = on ? Color.Gold : dgvPlan.DefaultCellStyle.BackColor;

            dgvPlan.Rows[row].DefaultCellStyle.BackColor = color;
            if (row + 1 < dgvPlan.Rows.Count)
                dgvPlan.Rows[row + 1].DefaultCellStyle.BackColor = color;
        }

        // 조건 통과했지만 팝업 대기 중 (연한 색)
        private void SetPlanColorQueued(TradePlanLine plan, bool on)
        {
            int row = plan.DisplayNo * 2 - 2;
            if (row < 0 || row >= dgvPlan.Rows.Count) return;

            var color = on ? Color.LightYellow : dgvPlan.DefaultCellStyle.BackColor;

            dgvPlan.Rows[row].DefaultCellStyle.BackColor = color;
            if (row + 1 < dgvPlan.Rows.Count)
                dgvPlan.Rows[row + 1].DefaultCellStyle.BackColor = color;
        }


    }
}
