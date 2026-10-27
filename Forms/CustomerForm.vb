Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Windows.Forms

''' <summary>Pages that can re-read their data when the order panel changes.</summary>
Public Interface IRefreshable
    Sub RefreshData()
End Interface

''' <summary>White navigation bar at the top of the customer side.</summary>
Public Class TopBar
    Inherits Control

    Public Event NavClicked As EventHandler(Of String)
    Public Event ProfileClicked As EventHandler

    Private ReadOnly _keys As String() = {"home", "products", "orders", "cart"}
    Private ReadOnly _labels As String() = {"Home", "Products", "My orders", "Cart"}
    Private ReadOnly _rects(3) As Rectangle
    Private _profileRect As Rectangle
    Private _active As String = "home"
    Private _hot As Integer = -1
    Private _profileHot As Boolean

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
        Height = 68
    End Sub

    Public Property ActiveKey As String
        Get
            Return _active
        End Get
        Set(value As String)
            _active = value
            Invalidate()
        End Set
    End Property

    Private Function RoleText() As String
        If Session.Role = "" Then Return ""
        Return Char.ToUpper(Session.Role(0)) & Session.Role.Substring(1)
    End Function

    Private Sub ComputeRects(g As Graphics)
        Dim nameW As Integer = TextRenderer.MeasureText(Theme.AppName, Theme.F(16, True), New Size(400, 60), TextFormatFlags.NoPadding).Width
        Dim x As Integer = 32 + 42 + 12 + nameW + 56
        For i As Integer = 0 To 3
            Dim tw As Integer = TextRenderer.MeasureText(_labels(i), Theme.F(10.5F, True), New Size(400, 60), TextFormatFlags.NoPadding).Width
            _rects(i) = New Rectangle(x, 14, tw + 36, 40)
            x += tw + 36 + 6
        Next
        Dim uw As Integer = Math.Min(180, TextRenderer.MeasureText(Session.Username, Theme.F(10.5F, True), New Size(400, 60), TextFormatFlags.NoPadding).Width)
        Dim rw As Integer = TextRenderer.MeasureText(RoleText(), Theme.F(9), New Size(400, 60), TextFormatFlags.NoPadding).Width
        Dim total As Integer = 4 + 44 + 12 + Math.Max(uw, rw) + 14
        _profileRect = New Rectangle(Width - 28 - total, 10, total, 48)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        Dim h As Integer = -1
        For i As Integer = 0 To 3
            If _rects(i).Contains(e.Location) Then h = i
        Next
        Dim overProfile As Boolean = _profileRect.Contains(e.Location)
        If h <> _hot OrElse overProfile <> _profileHot Then
            _hot = h
            _profileHot = overProfile
            Invalidate()
        End If
        Cursor = If(h >= 0 OrElse overProfile, Cursors.Hand, Cursors.Default)
    End Sub

    Protected Overrides Sub OnMouseLeave(e As EventArgs)
        MyBase.OnMouseLeave(e)
        _hot = -1
        _profileHot = False
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        For i As Integer = 0 To 3
            If _rects(i).Contains(e.Location) Then
                RaiseEvent NavClicked(Me, _keys(i))
                Return
            End If
        Next
        If _profileRect.Contains(e.Location) Then RaiseEvent ProfileClicked(Me, EventArgs.Empty)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(Color.White)
        g.SmoothingMode = SmoothingMode.AntiAlias
        ComputeRects(g)
        Using pen As New Pen(Theme.Border, 1)
            g.DrawLine(pen, 0, Height - 1, Width, Height - 1)
        End Using

        ' logo + app name
        Using tile As GraphicsPath = Theme.RoundRectPath(New Rectangle(32, 13, 42, 42), 14), br As New LinearGradientBrush(New Rectangle(32, 13, 42, 42), Color.FromArgb(255, 140, 70), Theme.Primary, 45.0F)
            g.FillPath(br, tile)
        End Using
        Icons.Draw(g, "logo", New Rectangle(41, 22, 24, 24), Color.White)
        TextRenderer.DrawText(g, Theme.AppName, Theme.F(16, True), New Rectangle(86, 0, 300, Height), Theme.TextDark, TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)

        ' nav
        For i As Integer = 0 To 3
            Dim sel As Boolean = (_keys(i) = _active)
            Dim r As Rectangle = _rects(i)
            If sel OrElse i = _hot Then
                Using path As GraphicsPath = Theme.RoundRectPath(r, 20), br As New SolidBrush(If(sel, Theme.PrimarySoft, Theme.FieldBg))
                    g.FillPath(br, path)
                End Using
            End If
            TextRenderer.DrawText(g, _labels(i), Theme.F(10.5F, True), r, If(sel, Theme.Primary, Theme.TextDark), TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding)
        Next

        ' profile (click = Account settings)
        Dim pr As Rectangle = _profileRect
        If _profileHot Then
            Using path As GraphicsPath = Theme.RoundRectPath(pr, 24), br As New SolidBrush(Theme.FieldBg)
                g.FillPath(br, path)
            End Using
        End If
        Avatars.Draw(g, New Rectangle(pr.X + 4, pr.Y + 2, 44, 44), Session.Username, ProfileSvc.MyAvatarPath())
        Dim tx As Integer = pr.X + 4 + 44 + 12
        TextRenderer.DrawText(g, Session.Username, Theme.F(10.5F, True), New Rectangle(tx, pr.Y + 4, pr.Right - tx - 10, 22), Theme.TextDark,
                              TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis Or TextFormatFlags.NoPrefix)
        TextRenderer.DrawText(g, RoleText(), Theme.F(9), New Rectangle(tx, pr.Y + 26, pr.Right - tx - 10, 18), Theme.TextMuted,
                              TextFormatFlags.VerticalCenter Or TextFormatFlags.NoPadding Or TextFormatFlags.EndEllipsis)
    End Sub
End Class

''' <summary>Main window for logged-in customers: top bar, swappable page, and the Your order panel.</summary>
Public Class CustomerForm
    Inherits Form

    Public LoggedOut As Boolean = False
    Public ReadOnly OrderPanelCtl As New OrderPanel()
    Private ReadOnly _top As New TopBar()
    Private _page As Control
    Private _showOrder As Boolean = False

    Public Sub New()
        Text = Theme.AppName
        Font = Theme.F(9.75F)
        BackColor = Theme.Bg
        StartPosition = FormStartPosition.CenterScreen
        ClientSize = New Size(1366, 800)
        MinimumSize = New Size(1180, 700)
        DoubleBuffered = True

        OrderPanelCtl.Visible = False
        Controls.Add(OrderPanelCtl)
        Controls.Add(_top)
        AddHandler Resize, Sub(s, e) LayoutShell()
        AddHandler _top.NavClicked, Sub(s, key) Nav(key)
        AddHandler _top.ProfileClicked, Sub(s, e) ShowAccount()
        AddHandler OrderPanelCtl.DataChanged, Sub(s, e) RefreshPage()
        AddHandler OrderPanelCtl.OrderPlaced, AddressOf OnOrderPlaced

        OrderPanelCtl.Reload()
        Swap(New HomePage(Me), "home", False)
    End Sub

    Private Function BodyRect() As Rectangle
        Dim w As Integer = ClientSize.Width
        If _showOrder Then w -= OrderPanel.PanelWidth
        Return New Rectangle(0, _top.Height, Math.Max(100, w), Math.Max(100, ClientSize.Height - _top.Height))
    End Function

    Private Sub LayoutShell()
        _top.SetBounds(0, 0, ClientSize.Width, 68)
        OrderPanelCtl.Visible = _showOrder
        If _showOrder Then
            OrderPanelCtl.SetBounds(ClientSize.Width - OrderPanel.PanelWidth, _top.Height, OrderPanel.PanelWidth, ClientSize.Height - _top.Height)
        End If
        If _page IsNot Nothing Then _page.Bounds = BodyRect()
    End Sub

    Private Sub Swap(page As Control, navKey As String, showOrder As Boolean)
        SuspendLayout()
        If _page IsNot Nothing Then
            Controls.Remove(_page)
            _page.Dispose()
        End If
        _page = page
        _showOrder = showOrder
        _top.ActiveKey = navKey
        Controls.Add(page)
        LayoutShell()
        If showOrder Then OrderPanelCtl.Reload()
        ResumeLayout(True)
        page.Focus()
    End Sub

    Private Sub Defer(action As Action)
        If IsHandleCreated Then
            BeginInvoke(action)
        Else
            action()
        End If
    End Sub

    Public Sub ShowHome()
        Defer(Sub() Swap(New HomePage(Me), "home", False))
    End Sub

    ''' <summary>stallId = 0 shows every product.</summary>
    Public Sub ShowProducts(stallId As Integer)
        Defer(Sub() Swap(New ProductsPage(Me, stallId), "products", True))
    End Sub

    Public Sub ShowProduct(productId As Integer, backStallId As Integer)
        Defer(Sub() Swap(New ProductDetailPage(Me, productId, backStallId), "products", True))
    End Sub

    Public Sub ShowOrders()
        Defer(Sub() Swap(New OrdersPage(Me), "orders", False))
    End Sub

    Public Sub ShowCart()
        Defer(Sub() Swap(New CartPage(Me), "cart", True))
    End Sub

    ''' <summary>Account settings (opened by clicking the profile at the top right).</summary>
    Public Sub ShowAccount()
        Defer(Sub()
                  Dim p As New AccountPage()
                  AddHandler p.Saved, Sub() _top.Invalidate()
                  AddHandler p.LogoutRequested, AddressOf DoLogout
                  Swap(p, "", False)
              End Sub)
    End Sub

    Private Sub Nav(key As String)
        Select Case key
            Case "home" : ShowHome()
            Case "products" : ShowProducts(0)
            Case "orders" : ShowOrders()
            Case "cart" : ShowCart()
        End Select
    End Sub

    Private Sub RefreshPage()
        Dim r As IRefreshable = TryCast(_page, IRefreshable)
        If r IsNot Nothing Then r.RefreshData()
    End Sub

    Private Sub OnOrderPlaced(sender As Object, orderId As Long)
        ShowOrders()
        Toast.Show(Me, "Order #" & orderId.ToString() & " placed! Pay at each stall's counter.")
    End Sub

    Private Sub DoLogout()
        If Ui.Confirm("Log out of " & Theme.AppName & "?") Then
            LoggedOut = True
            Close()
        End If
    End Sub

    ''' <summary>Quick toast helper for pages.</summary>
    Public Sub Notify(message As String, Optional isError As Boolean = False)
        Toast.Show(Me, message, isError)
    End Sub
End Class
