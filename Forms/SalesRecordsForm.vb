Imports System.Data
Imports System.Drawing
Imports System.Globalization
Imports System.IO
Imports System.Text
Imports System.Windows.Forms

''' <summary>Popup with the stall's sales (completed orders only): range filter, totals, top items, order list and CSV export.</summary>
Public Class SalesRecordsForm
    Inherits Form

    Private ReadOnly _stallId As Integer
    Private ReadOnly _stallName As String
    Private ReadOnly _range(3) As RoundedButton
    Private ReadOnly _export As RoundedButton
    Private ReadOnly _revenue As Label
    Private ReadOnly _orders As Label
    Private ReadOnly _items As Label
    Private ReadOnly _topCard As New RoundedPanel()
    Private ReadOnly _list As New ListView()
    Private ReadOnly _empty As Label
    Private _sel As Integer = 1
    Private _table As DataTable

    Public Sub New(stallId As Integer, stallName As String)
        _stallId = stallId
        _stallName = stallName
        Text = "Sales records"
        Font = Theme.F(9.75F)
        BackColor = Theme.Bg
        FormBorderStyle = FormBorderStyle.FixedDialog
        MaximizeBox = False
        MinimizeBox = False
        ShowInTaskbar = False
        StartPosition = FormStartPosition.CenterParent
        ClientSize = New Size(960, 790)

        Dim pad As Integer = 24
        Dim inner As Integer = 960 - pad * 2

        Dim title As Label = Ui.Lbl("Sales records", 20, True)
        title.Location = New Point(pad, 18)
        Dim sub1 As Label = Ui.Lbl("Completed orders of " & stallName & ". Declined orders are not counted.", 10.5F, False, Theme.TextMuted)
        sub1.Location = New Point(pad, 56)
        Controls.Add(title)
        Controls.Add(sub1)

        Dim names() As String = {"Today", "Last 7 days", "Last 30 days", "All time"}
        Dim x As Integer = pad
        For i As Integer = 0 To 3
            Dim idx As Integer = i
            _range(i) = Ui.OutlineBtn(names(i), If(i >= 2, 130, 120), 42)
            _range(i).Location = New Point(x, 92)
            AddHandler _range(i).Click, Sub(s, e) SelectRange(idx)
            Controls.Add(_range(i))
            x += _range(i).Width + 10
        Next
        _export = Ui.SoftBtn("Export CSV", 140, 42)
        _export.Location = New Point(pad + inner - 140, 92)
        AddHandler _export.Click, AddressOf OnExport
        Controls.Add(_export)

        ' totals
        Dim tw As Integer = (inner - 28) \ 3
        _revenue = AddTile("Revenue", pad, 150, tw)
        _orders = AddTile("Completed orders", pad + tw + 14, 150, tw)
        _items = AddTile("Items sold", pad + (tw + 14) * 2, 150, tw)

        ' top items
        _topCard.Radius = 16
        _topCard.FillColor = Color.White
        _topCard.SurroundColor = Theme.Bg
        _topCard.Shadow = True
        _topCard.SetBounds(pad, 258, inner, 182)
        Controls.Add(_topCard)

        ' order list
        Dim lt As Label = Ui.Lbl("Orders", 13, True)
        lt.Location = New Point(pad, 456)
        Controls.Add(lt)
        _list.View = View.Details
        _list.FullRowSelect = True
        _list.GridLines = False
        _list.HideSelection = False
        _list.BorderStyle = BorderStyle.None
        _list.Font = Theme.F(10)
        _list.Columns.Add("Order", 80)
        _list.Columns.Add("Customer", 140)
        _list.Columns.Add("Items", 400)
        _list.Columns.Add("Total", 100, HorizontalAlignment.Right)
        _list.Columns.Add("Completed", 170)
        _list.SetBounds(pad, 488, inner, 278)
        Controls.Add(_list)
        _empty = Ui.Lbl("No completed orders in this period.", 11, False, Theme.TextMuted)
        _empty.BackColor = Color.White
        _empty.Location = New Point(pad + 20, 540)
        Controls.Add(_empty)
        _empty.BringToFront()

        SelectRange(1)
    End Sub

    Private Function AddTile(caption As String, x As Integer, y As Integer, w As Integer) As Label
        Dim tile As New RoundedPanel()
        tile.Radius = 16
        tile.FillColor = Color.White
        tile.SurroundColor = Theme.Bg
        tile.Shadow = True
        tile.SetBounds(x, y, w, 92)
        Dim cap As Label = Ui.Lbl(caption, 10.5F, False, Theme.TextMuted)
        cap.Location = New Point(20, 16)
        tile.Controls.Add(cap)
        Dim val As Label = Ui.Lbl("-", 21, True, Theme.TextDark)
        val.Location = New Point(20, 42)
        tile.Controls.Add(val)
        Controls.Add(tile)
        Return val
    End Function

    Private Shared Function RangeStart(sel As Integer) As DateTime
        Select Case sel
            Case 0 : Return DateTime.Today
            Case 1 : Return DateTime.Today.AddDays(-6)
            Case 2 : Return DateTime.Today.AddDays(-29)
        End Select
        Return New DateTime(2000, 1, 1)
    End Function

    Private Sub SelectRange(i As Integer)
        _sel = i
        For k As Integer = 0 To 3
            Dim b As RoundedButton = _range(k)
            If k = i Then
                b.FillColor = Theme.Primary
                b.HoverColor = Theme.PrimaryDark
                b.TextColor = Color.White
                b.BorderColor = Theme.Primary
            Else
                b.FillColor = Color.White
                b.HoverColor = Theme.PrimarySoft
                b.TextColor = Theme.TextDark
                b.BorderColor = Theme.Border
            End If
            b.Invalidate()
        Next
        LoadData()
    End Sub

    Private Sub LoadData()
        Dim startDate As DateTime = RangeStart(_sel)
        Try
            Dim t As DataRow = StoreSvc.GetSalesTotals(_stallId, startDate)
            _revenue.Text = Theme.Peso(Db.GetDec(t, "revenue"))
            _orders.Text = Db.GetInt(t, "orders").ToString()
            _items.Text = Db.GetInt(t, "items").ToString()
            BuildTop(StoreSvc.GetTopItems(_stallId, startDate))
            _table = StoreSvc.GetSalesOrders(_stallId, startDate)
        Catch ex As Exception
            _table = New DataTable()
            Ui.Warn("Could not load the sales records: " & ex.Message)
        End Try
        FillList()
    End Sub

    Private Sub BuildTop(dt As DataTable)
        Ui.DisposeChildren(_topCard)
        Dim head As Label = Ui.Lbl("Top items", 13, True)
        head.Location = New Point(20, 14)
        _topCard.Controls.Add(head)
        If dt.Rows.Count = 0 Then
            Dim none As Label = Ui.Lbl("Nothing sold in this period yet.", 10.5F, False, Theme.TextMuted)
            none.Location = New Point(20, 52)
            _topCard.Controls.Add(none)
            Return
        End If
        Dim y As Integer = 46
        Dim n As Integer = 1
        Dim w As Integer = _topCard.Width
        For Each r As DataRow In dt.Rows
            Dim nm As Label = Ui.FixedLbl(n.ToString() & ".  " & Db.GetStr(r, "product_name"), 10.5F, w - 330, False, Theme.TextDark)
            nm.Location = New Point(20, y)
            _topCard.Controls.Add(nm)
            Dim q As Label = Ui.Lbl(Db.GetInt(r, "qty").ToString() & " sold", 10.5F, False, Theme.TextMuted)
            q.Location = New Point(w - 290, y)
            _topCard.Controls.Add(q)
            Dim rv As New Label()
            rv.AutoSize = False
            rv.TextAlign = ContentAlignment.MiddleRight
            rv.Font = Theme.F(10.5F, True)
            rv.ForeColor = Theme.Primary
            rv.Text = Theme.Peso(Db.GetDec(r, "revenue"))
            rv.SetBounds(w - 20 - 140, y - 2, 140, 24)
            _topCard.Controls.Add(rv)
            y += 26
            n += 1
        Next
    End Sub

    Private Sub FillList()
        _list.BeginUpdate()
        _list.Items.Clear()
        For Each r As DataRow In _table.Rows
            Dim it As New ListViewItem("#" & Db.GetInt(r, "order_id").ToString())
            it.SubItems.Add(Db.GetStr(r, "username"))
            it.SubItems.Add(Db.GetStr(r, "items"))
            it.SubItems.Add(Theme.Peso(Db.GetDec(r, "subtotal")))
            it.SubItems.Add(Db.GetDate(r, "done_at").ToString("MMM d, yyyy  h:mm tt"))
            _list.Items.Add(it)
        Next
        _list.EndUpdate()
        _empty.Visible = (_table.Rows.Count = 0)
        _export.Enabled = (_table.Rows.Count > 0)
    End Sub

    Private Shared Function Csv(s As String) As String
        Return """" & If(s, "").Replace("""", """""") & """"
    End Function

    Private Sub OnExport(sender As Object, e As EventArgs)
        If _table Is Nothing OrElse _table.Rows.Count = 0 Then
            Ui.Info("There is nothing to export for this period.")
            Return
        End If
        Using dlg As New SaveFileDialog()
            Dim safe As String = _stallName
            For Each c As Char In Path.GetInvalidFileNameChars()
                safe = safe.Replace(c, "_"c)
            Next
            dlg.Title = "Export sales records"
            dlg.Filter = "CSV file (*.csv)|*.csv"
            dlg.FileName = "sales-" & safe & "-" & DateTime.Today.ToString("yyyy-MM-dd") & ".csv"
            If dlg.ShowDialog(Me) <> DialogResult.OK Then Return
            Try
                Dim sb As New StringBuilder()
                sb.AppendLine("Order,Customer,Items,Total,Completed")
                For Each r As DataRow In _table.Rows
                    Dim cols As New List(Of String)
                    cols.Add(Csv("#" & Db.GetInt(r, "order_id").ToString()))
                    cols.Add(Csv(Db.GetStr(r, "username")))
                    cols.Add(Csv(Db.GetStr(r, "items")))
                    cols.Add(Db.GetDec(r, "subtotal").ToString("0.00", CultureInfo.InvariantCulture))
                    cols.Add(Csv(Db.GetDate(r, "done_at").ToString("yyyy-MM-dd HH:mm")))
                    sb.AppendLine(String.Join(",", cols))
                Next
                File.WriteAllText(dlg.FileName, sb.ToString(), New UTF8Encoding(True))
                Ui.Info("Saved " & _table.Rows.Count.ToString() & " orders to " & dlg.FileName)
            Catch ex As Exception
                Ui.Warn("Could not save the file: " & ex.Message)
            End Try
        End Using
    End Sub
End Class
