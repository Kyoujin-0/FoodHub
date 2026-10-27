Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Admin window: top bar (Stores / Rent / Sellers / Banners, profile) and the current page below it.</summary>
Public Class AdminForm
    Inherits Form

    Public LoggedOut As Boolean

    Private ReadOnly _bar As New AdminTopBar()
    Private _page As Control

    Public Sub New()
        Text = Theme.AppName & " - Admin"
        BackColor = Theme.Bg
        Font = Theme.F(10)
        MinimumSize = New Size(1200, 720)
        Size = New Size(1366, 820)
        StartPosition = FormStartPosition.CenterScreen
        DoubleBuffered = True

        Controls.Add(_bar)
        AddHandler _bar.TabClicked, AddressOf ShowTab
        AddHandler _bar.ProfileClicked, AddressOf ShowAccount
        ShowTab(0) ' admin opens on Stores
    End Sub

    Private Sub DoLayoutAll()
        _bar.SetBounds(32, 14, Math.Max(200, ClientSize.Width - 64), 78)
        If _page IsNot Nothing Then
            _page.SetBounds(0, 96, ClientSize.Width, Math.Max(100, ClientSize.Height - 96))
        End If
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        DoLayoutAll()
    End Sub

    Private Sub ClearPage()
        If _page IsNot Nothing Then
            Controls.Remove(_page)
            _page.Dispose()
            _page = Nothing
        End If
    End Sub

    Private Sub ShowTab(index As Integer)
        ClearPage()
        Select Case index
            Case 0
                Dim sp As New AdminStoresPage()
                AddHandler sp.RecordPaymentRequested, AddressOf OnRecordPaymentRequested
                _page = sp
            Case 1 : _page = New AdminRentPage()
            Case 2 : _page = New AdminSellersPage()
            Case Else : _page = New AdminBannersPage()
        End Select
        Controls.Add(_page)
        _bar.SetActive(index)
        DoLayoutAll()
    End Sub

    ''' <summary>Stores tab: "Record payment" opens the Rent tab with that stall selected.</summary>
    Private Sub OnRecordPaymentRequested(stallId As Integer)
        ' run after the click handler ends, because ShowTab disposes the Stores page
        BeginInvoke(New Action(Sub()
                                   ShowTab(1)
                                   Dim rp = TryCast(_page, AdminRentPage)
                                   If rp IsNot Nothing Then rp.SelectStall(stallId)
                               End Sub))
    End Sub

    ''' <summary>Account settings (photo, username, password, log out), opened from the profile in the top bar.</summary>
    Private Sub ShowAccount()
        ClearPage()
        Dim p As New AccountPage()
        AddHandler p.Saved, Sub() _bar.Invalidate()
        AddHandler p.LogoutRequested, AddressOf OnLogout
        _page = p
        Controls.Add(_page)
        _bar.SetActive(-1)
        DoLayoutAll()
    End Sub

    Private Sub OnLogout()
        If Not Ui.Confirm("Log out of the admin account?") Then Return
        LoggedOut = True
        Close()
    End Sub
End Class

''' <summary>Rounded white bar: app logo + name, Stores/Rent/Sellers/Banners tabs, admin profile (opens Account settings).</summary>
Public Class AdminTopBar
    Inherits Control

    Public Event TabClicked(index As Integer)
    Public Event ProfileClicked()

    Private ReadOnly _names() As String = {"Stores", "Rent", "Sellers", "Banners"}
    Private ReadOnly _rects(3) As Rectangle
    Private ReadOnly _fName As Font = Theme.F(15, True)
    Private ReadOnly _fTab As Font = Theme.F(12)
    Private ReadOnly _fTabBold As Font = Theme.F(12, True)
    Private ReadOnly _fUser As Font = Theme.F(10.5F, True)
    Private ReadOnly _fRole As Font = Theme.F(9)
    Private _profileRect As Rectangle
    Private _compactProfile As Boolean
    Private _active As Integer
    Private _hot As Integer = -1 ' 0-3 tabs, 11 profile

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Theme.Bg
    End Sub

    Public Sub SetActive(index As Integer)
        _active = index
        Invalidate()
    End Sub

    Private Sub LayoutItems()
        Dim barH As Integer = Height - 6
        Dim x As Integer = 24 + 54 + 14
        Dim nameW As Integer = Math.Min(140, TextW(Theme.AppName, _fName))
        x += nameW + 28
        For i As Integer = 0 To 3
            Dim f As Font = If(i = _active, _fTabBold, _fTab)
            Dim w As Integer = If(i = 1, 20, 12 + 22 + 8) + TextW(_names(i), f) + 16
            _rects(i) = New Rectangle(x, (barH - 54) \ 2, w, 54)
            x += w + 6
        Next
        Dim userW As Integer = Math.Min(130, TextW(Session.Username, _fUser))
        Dim fullW As Integer = 44 + 10 + userW + 8
        Dim avail As Integer = Width - 24 - (_rects(3).Right + 12)
        _compactProfile = (avail < fullW)
        Dim pw As Integer = If(_compactProfile, 44, fullW)
        _profileRect = New Rectangle(Width - 24 - pw, (barH - 48) \ 2, pw, 48)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        LayoutItems()
        Dim barH As Integer = Height - 6
        Using sh As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 3, Width - 1, barH), 28), sb As New SolidBrush(Color.FromArgb(18, 90, 50, 20))
            g.FillPath(sb, sh)
        End Using
        Using p As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, barH), 28)
            g.FillPath(Brushes.White, p)
        End Using

        ' app logo + name
        Dim lr As New Rectangle(24, (barH - 54) \ 2, 54, 54)
        Using b As New SolidBrush(Theme.PrimarySoft)
            g.FillEllipse(b, lr)
        End Using
        Icons.Draw(g, "logo", New Rectangle(lr.X + 11, lr.Y + 11, 32, 32), Theme.Primary)
        Dim nameW As Integer = Math.Min(140, TextW(Theme.AppName, _fName))
        TextRenderer.DrawText(g, Theme.AppName, _fName, New Rectangle(24 + 54 + 14, (barH - 30) \ 2, nameW + 4, 30), Theme.TextDark, TxtLeft)

        ' tabs
        For i As Integer = 0 To 3
            Dim r As Rectangle = _rects(i)
            Dim isActive As Boolean = (i = _active)
            If isActive OrElse i = _hot Then
                Using p As GraphicsPath = Theme.RoundRectPath(r, 27), b As New SolidBrush(If(isActive, Theme.PrimarySoft, Color.FromArgb(250, 243, 236)))
                    g.FillPath(b, p)
                End Using
            End If
            Dim col As Color = If(isActive, Theme.Primary, Theme.TextDark)
            Dim ir As New Rectangle(r.X + 12, r.Y + (r.Height - 22) \ 2, 22, 22)
            Select Case i
                Case 0 : Icons.Draw(g, "home", ir, col)
                Case 1 ' Rent: text only, no icon
                Case 2 : Icons.Draw(g, "user", ir, col)
                Case Else : DrawBannerIcon(g, ir, col)
            End Select
            Dim textX As Integer = If(i = 1, r.X + 20, ir.Right + 8)
            TextRenderer.DrawText(g, _names(i), If(isActive, _fTabBold, _fTab), New Rectangle(textX, r.Y, r.Width, r.Height), col, TxtLeft)
        Next

        ' profile
        Dim pr As Rectangle = _profileRect
        If _hot = 11 Then
            Dim hr As Rectangle = Rectangle.Inflate(pr, 6, 4)
            Using p As GraphicsPath = Theme.RoundRectPath(hr, 26), b As New SolidBrush(Color.FromArgb(250, 243, 236))
                g.FillPath(b, p)
            End Using
        End If
        Avatars.Draw(g, New Rectangle(pr.X, pr.Y + 2, 44, 44), Session.Username, ProfileSvc.MyAvatarPath())
        If Not _compactProfile Then
            TextRenderer.DrawText(g, Session.Username, _fUser, New Rectangle(pr.X + 54, pr.Y + 4, pr.Width - 54, 22), Theme.TextDark, TxtLeft)
            TextRenderer.DrawText(g, "Admin", _fRole, New Rectangle(pr.X + 54, pr.Y + 26, pr.Width - 54, 18), Theme.TextMuted, TxtLeft)
        End If
    End Sub

    Private Sub DrawBannerIcon(g As Graphics, r As Rectangle, c As Color)
        Using pen As New Pen(c, 2.0F)
            pen.LineJoin = LineJoin.Round
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            g.DrawRectangle(pen, r.X + 1, r.Y + 3, r.Width - 3, r.Height - 7)
            g.DrawLines(pen, New Point() {New Point(r.X + 4, r.Bottom - 6), New Point(r.X + 9, r.Y + 11), New Point(r.X + 13, r.Bottom - 8), New Point(r.X + 16, r.Y + 13), New Point(r.Right - 4, r.Bottom - 6)})
        End Using
    End Sub

    Private Function HitTest(p As Point) As Integer
        LayoutItems()
        For i As Integer = 0 To 3
            If _rects(i).Contains(p) Then Return i
        Next
        If Rectangle.Inflate(_profileRect, 6, 4).Contains(p) Then Return 11
        Return -1
    End Function

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim h As Integer = HitTest(e.Location)
        If h <> _hot Then
            _hot = h
            Cursor = If(h >= 0, Cursors.Hand, Cursors.Default)
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        If _hot <> -1 Then
            _hot = -1
            Cursor = Cursors.Default
            Invalidate()
        End If
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        Dim h As Integer = HitTest(e.Location)
        If h >= 0 AndAlso h <= 3 Then
            If h <> _active Then RaiseEvent TabClicked(h)
        ElseIf h = 11 Then
            RaiseEvent ProfileClicked()
        End If
    End Sub
End Class
