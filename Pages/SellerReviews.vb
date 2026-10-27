Imports System.Data
Imports System.Drawing
Imports System.Text
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Seller "Reviews" tab: rating overview, filters, and every review of the stall's items with a one-time reply.</summary>
Public Class SellerReviewsPage
    Inherits PageBase

    Private Const PageSize As Integer = 20

    Private ReadOnly _stallId As Integer
    Private ReadOnly _stallName As String
    Private ReadOnly _title As Label
    Private ReadOnly _subtitle As Label
    Private ReadOnly _box As New ReviewSummaryBox()
    Private ReadOnly _tabs As New FilterTabs()
    Private ReadOnly _search As New AppTextBox()
    Private ReadOnly _empty As Label
    Private ReadOnly _more As RoundedButton
    Private ReadOnly _cards As New List(Of ReviewCard)
    Private ReadOnly _poll As New Timer()
    Private _all As New DataTable()
    Private _signature As String = ""
    Private _shown As Integer = PageSize
    Private _matches As Integer
    Private _failed As Boolean

    Public Sub New(stallId As Integer, stallName As String)
        _stallId = stallId
        _stallName = stallName

        _title = Ui.Lbl("Reviews", 26, True)
        _subtitle = Ui.Lbl("Customers rate the items you sell. You can reply once to each review, and a reply is final, so it never turns into a long conversation.", 11, False, Theme.TextMuted)
        _search.LeftIcon = "search"
        _search.PlaceholderText = "Search item, customer or comment"
        _search.Font = Theme.F(11)
        _search.MaxLength = 40
        _empty = Ui.Lbl("", 11, False, Theme.TextMuted)
        _more = Ui.SoftBtn("Show more reviews", 230, 48)
        _more.Font = Theme.F(11, True)
        _more.Visible = False

        Controls.Add(_title)
        Controls.Add(_subtitle)
        Controls.Add(_box)
        Controls.Add(_tabs)
        Controls.Add(_search)
        Controls.Add(_empty)
        Controls.Add(_more)

        AddHandler _tabs.SelectedChanged, AddressOf OnFilterChanged
        AddHandler _search.TextChanged, AddressOf OnFilterChanged
        AddHandler _more.Click, AddressOf OnMore

        LoadData()
        _poll.Interval = 10000
        AddHandler _poll.Tick, AddressOf OnPoll
        _poll.Start()
    End Sub

    Private Shared Function MakeSignature(dt As DataTable) As String
        Dim sb As New StringBuilder()
        For Each r As DataRow In dt.Rows
            sb.Append(Db.GetInt(r, "id")).Append(":").Append(Db.GetInt(r, "rating")).Append(":")
            sb.Append(If(Db.GetStr(r, "reply") = "", "0", "1")).Append(":").Append(Db.GetStr(r, "comment").Length).Append(";")
        Next
        Return sb.ToString()
    End Function

    Private Sub LoadData()
        Try
            _all = ReviewSvc.GetStallReviews(_stallId)
            _box.SetData(ReviewSvc.GetStallSummary(_stallId))
            _failed = False
        Catch ex As Exception
            _failed = True
            _all = New DataTable()
            _box.SetData(New ReviewSummary())
        End Try
        _signature = MakeSignature(_all)
        UpdateTabs()
        BuildCards()
    End Sub

    Private Sub UpdateTabs()
        Dim needs As Integer = 0
        For Each r As DataRow In _all.Rows
            If Db.GetStr(r, "reply") = "" Then needs += 1
        Next
        Dim names As New List(Of String)
        names.Add("All")
        names.Add("Needs reply")
        names.Add("Replied")
        Dim counts As New List(Of Integer)
        counts.Add(_all.Rows.Count)
        counts.Add(needs)
        counts.Add(_all.Rows.Count - needs)
        _tabs.SetItems(names, counts, _tabs.SelectedIndex)
    End Sub

    Private Sub BuildCards()
        For Each c As ReviewCard In _cards
            Controls.Remove(c)
            c.Dispose()
        Next
        _cards.Clear()

        Dim q As String = _search.Text.Trim().ToLower()
        Dim mode As Integer = _tabs.SelectedIndex
        Dim matches As New List(Of DataRow)
        For Each r As DataRow In _all.Rows
            Dim replied As Boolean = (Db.GetStr(r, "reply") <> "")
            If mode = 1 AndAlso replied Then Continue For
            If mode = 2 AndAlso Not replied Then Continue For
            If q <> "" Then
                Dim hay As String = (Db.GetStr(r, "product_name") & " " & Db.GetStr(r, "username") & " " & Db.GetStr(r, "comment")).ToLower()
                If Not hay.Contains(q) Then Continue For
            End If
            matches.Add(r)
        Next
        _matches = matches.Count

        For i As Integer = 0 To Math.Min(_shown, matches.Count) - 1
            Dim d As ReviewData = ReviewData.FromRow(matches(i), False, _stallName)
            Dim card As New ReviewCard(d, True, True, True)
            AddHandler card.ReplySubmitted, AddressOf OnReply
            _cards.Add(card)
            Controls.Add(card)
        Next

        If _failed Then
            _empty.Text = "Reviews could not be loaded. Run patch_reviews.sql on your database."
        ElseIf _all.Rows.Count = 0 Then
            _empty.Text = "No reviews yet. When customers rate your items, they will show up here."
        ElseIf matches.Count = 0 Then
            _empty.Text = If(mode = 1, "Nothing waiting for a reply. Nice!", "No reviews match your filter.")
        Else
            _empty.Text = ""
        End If
        _more.Visible = (matches.Count > _cards.Count)
    End Sub

    Private Sub OnFilterChanged(sender As Object, e As EventArgs)
        _shown = PageSize
        BuildCards()
        RelayoutKeepScroll()
    End Sub

    Private Sub OnMore(sender As Object, e As EventArgs)
        _shown += PageSize
        BuildCards()
        RelayoutKeepScroll()
    End Sub

    Private Sub OnReply(card As ReviewCard, text As String)
        If Not Ui.Confirm("Send this reply to " & card.Data.Username & "? A review can only be answered once and you won't be able to edit the reply afterwards.") Then Return
        Dim err As String = ""
        Dim ok As Boolean = False
        Try
            ok = ReviewSvc.ReplyTo(card.Data.Id, text, err)
        Catch ex As Exception
            Ui.Warn("Could not send the reply: " & ex.Message)
            Return
        End Try
        If Not ok Then Ui.Warn(err)
        ' the card that raised the event is rebuilt, so wait until its click has finished
        BeginInvoke(New Action(Of Boolean)(AddressOf AfterReply), ok)
    End Sub

    Private Sub AfterReply(ok As Boolean)
        LoadData()
        RelayoutKeepScroll()
        If ok Then Toast.Show(FindForm(), "Reply sent")
    End Sub

    Private Sub OnPoll(sender As Object, e As EventArgs)
        Try
            For Each c As ReviewCard In _cards
                If c.HasDraft Then Return
            Next
            Dim dt As DataTable = ReviewSvc.GetStallReviews(_stallId)
            If MakeSignature(dt) = _signature Then Return
            LoadData()
            RelayoutKeepScroll()
        Catch
        End Try
    End Sub

    Protected Overrides Sub LayoutPage(contentWidth As Integer)
        Dim y As Integer = 30
        _title.Location = New Point(M, y)
        y += 50
        _subtitle.Location = New Point(M, y)
        Ui.FitLabel(_subtitle, Math.Min(contentWidth, 760))
        y += _subtitle.Height + 18

        _box.SetBounds(M, y, Math.Min(contentWidth, 640), 172)
        y += 172 + 24

        Dim tabsW As Integer = 440
        _tabs.SetBounds(M, y, Math.Min(contentWidth, tabsW), 44)
        If contentWidth - tabsW - 16 >= 200 Then
            Dim sw As Integer = Math.Min(320, contentWidth - tabsW - 16)
            _search.SetBounds(M + contentWidth - sw, y - 1, sw, 46)
            y += 44 + 22
        Else
            y += 44 + 12
            _search.SetBounds(M, y, contentWidth, 46)
            y += 46 + 22
        End If

        For Each c As ReviewCard In _cards
            c.ApplyWidth(contentWidth)
            c.SetBounds(M, y, contentWidth, c.Height)
            y += c.Height + 16
        Next

        _empty.Visible = (_empty.Text <> "")
        If _empty.Visible Then
            _empty.Location = New Point(M + 4, y + 4)
            y += 40
        End If
        If _more.Visible Then
            _more.Location = New Point(M + (contentWidth - _more.Width) \ 2, y + 6)
            y += 6 + _more.Height + 10
        End If
        SetContentHeight(y + 30)
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            _poll.Stop()
            _poll.Dispose()
        End If
        MyBase.Dispose(disposing)
    End Sub
End Class
