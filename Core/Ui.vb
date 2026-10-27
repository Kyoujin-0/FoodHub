Imports System.Drawing
Imports System.Drawing.Drawing2D
Imports System.Runtime.InteropServices
Imports System.Windows.Forms
Imports Timer = System.Windows.Forms.Timer

''' <summary>Small factory/helper functions so pages stay short.</summary>
Public Module Ui
    Private ReadOnly _imgCache As New Dictionary(Of String, Image)

    ''' <summary>Loads an image from disk (relative paths are relative to the .exe). Returns Nothing when missing.</summary>
    Public Function LoadImage(path As String) As Image
        If String.IsNullOrWhiteSpace(path) Then Return Nothing
        Try
            Dim full As String = path
            If Not System.IO.Path.IsPathRooted(full) Then full = System.IO.Path.Combine(Db.DataFolder(), path)
            If _imgCache.ContainsKey(full) Then Return _imgCache(full)
            If Not System.IO.File.Exists(full) Then Return Nothing
            Dim img As Image = Image.FromStream(New System.IO.MemoryStream(System.IO.File.ReadAllBytes(full)))
            _imgCache(full) = img
            Return img
        Catch
            Return Nothing
        End Try
    End Function

    ''' <summary>Auto-sized single line label.</summary>
    Public Function Lbl(text As String, size As Single, Optional bold As Boolean = False, Optional color As Color = Nothing) As Label
        Dim l As New Label()
        l.AutoSize = True
        l.Text = text
        l.Font = Theme.F(size, bold)
        If color.IsEmpty Then color = Theme.TextDark
        l.ForeColor = color
        l.UseMnemonic = False
        Return l
    End Function

    ''' <summary>Fixed-width label that word-wraps; height is measured from the text.</summary>
    Public Function WrapLbl(text As String, size As Single, width As Integer, Optional color As Color = Nothing, Optional bold As Boolean = False) As Label
        Dim l As New Label()
        l.AutoSize = False
        l.Text = text
        l.Font = Theme.F(size, bold)
        If color.IsEmpty Then color = Theme.TextDark
        l.ForeColor = color
        l.UseMnemonic = False
        Dim h As Integer = TextRenderer.MeasureText(text, l.Font, New Size(Math.Max(20, width), 0), TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding).Height
        l.SetBounds(0, 0, width, h + 4)
        Return l
    End Function

    ''' <summary>Set a wrapping label to a width and grow its height to fit the text.</summary>
    Public Sub FitLabel(l As Label, width As Integer)
        l.AutoSize = False
        Dim h As Integer = TextRenderer.MeasureText(l.Text, l.Font, New Size(Math.Max(20, width), 0), TextFormatFlags.WordBreak Or TextFormatFlags.NoPadding).Height
        l.SetBounds(l.Left, l.Top, width, h + 4)
    End Sub

    ''' <summary>Fixed-size single line label with "..." when too long.</summary>
    Public Function FixedLbl(text As String, size As Single, width As Integer, Optional bold As Boolean = False, Optional color As Color = Nothing) As Label
        Dim l As New Label()
        l.AutoSize = False
        l.AutoEllipsis = True
        l.Text = text
        l.Font = Theme.F(size, bold)
        If color.IsEmpty Then color = Theme.TextDark
        l.ForeColor = color
        l.UseMnemonic = False
        l.SetBounds(0, 0, width, l.Font.Height + 4)
        Return l
    End Function

    ''' <summary>Clickable text link.</summary>
    Public Function Link(text As String, size As Single, Optional color As Color = Nothing, Optional bold As Boolean = True) As Label
        If color.IsEmpty Then color = Theme.Primary
        Dim l As Label = Lbl(text, size, bold, color)
        l.Cursor = Cursors.Hand
        Dim normal As Font = l.Font
        Dim hot As New Font(normal, normal.Style Or FontStyle.Underline)
        AddHandler l.MouseEnter, Sub(s, e) l.Font = hot
        AddHandler l.MouseLeave, Sub(s, e) l.Font = normal
        Return l
    End Function

    Public Function PrimaryBtn(text As String, w As Integer, h As Integer) As RoundedButton
        Dim b As New RoundedButton()
        b.Text = text
        b.Size = New Size(w, h)
        Return b
    End Function

    Public Function OutlineBtn(text As String, w As Integer, h As Integer) As RoundedButton
        Dim b As New RoundedButton()
        b.Text = text
        b.Size = New Size(w, h)
        b.FillColor = Color.White
        b.HoverColor = Theme.PrimarySoft
        b.TextColor = Theme.Primary
        b.BorderColor = Theme.Primary
        Return b
    End Function

    Public Function SoftBtn(text As String, w As Integer, h As Integer) As RoundedButton
        Dim b As New RoundedButton()
        b.Text = text
        b.Size = New Size(w, h)
        b.FillColor = Theme.PrimarySoft
        b.HoverColor = Color.FromArgb(255, 226, 212)
        b.TextColor = Theme.Primary
        Return b
    End Function

    Public Function Ellipsize(text As String, maxChars As Integer) As String
        If text Is Nothing Then Return ""
        If text.Length <= maxChars Then Return text
        Return text.Substring(0, Math.Max(0, maxChars - 1)).TrimEnd() & ChrW(&H2026)
    End Function

    ''' <summary>Hook the same click handler to a control and all of its children (not buttons you wired yourself).</summary>
    Public Sub ClickAll(c As Control, h As EventHandler)
        AddHandler c.Click, h
        c.Cursor = Cursors.Hand
        For Each child As Control In c.Controls
            ClickAll(child, h)
        Next
    End Sub

    Public Sub DisposeChildren(parent As Control)
        Dim list As New List(Of Control)
        For Each c As Control In parent.Controls
            list.Add(c)
        Next
        parent.Controls.Clear()
        For Each c As Control In list
            c.Dispose()
        Next
    End Sub

    Public Sub Info(message As String, Optional title As String = "")
        MessageBox.Show(message, If(title = "", Theme.AppName, title), MessageBoxButtons.OK, MessageBoxIcon.Information)
    End Sub

    Public Sub Warn(message As String, Optional title As String = "")
        MessageBox.Show(message, If(title = "", Theme.AppName, title), MessageBoxButtons.OK, MessageBoxIcon.Warning)
    End Sub

    Public Function Confirm(message As String, Optional title As String = "") As Boolean
        Return MessageBox.Show(message, If(title = "", Theme.AppName, title), MessageBoxButtons.YesNo, MessageBoxIcon.Question) = DialogResult.Yes
    End Function

    ' ---------- order status helpers ----------
    Public Function StatusText(status As String) As String
        Select Case status
            Case "pending" : Return "Order placed"
            Case "preparing" : Return "Preparing"
            Case "ready" : Return "Ready to pickup"
            Case "completed" : Return "Completed"
            Case "cancelled" : Return "Cancelled"
        End Select
        Return status
    End Function

    Public Function StatusFore(status As String) As Color
        Select Case status
            Case "pending" : Return Theme.Info
            Case "preparing" : Return Theme.Warning
            Case "ready" : Return Theme.Success
            Case "completed" : Return Color.FromArgb(90, 100, 94)
            Case "cancelled" : Return Theme.Danger
        End Select
        Return Theme.TextMuted
    End Function

    Public Function StatusBack(status As String) As Color
        Select Case status
            Case "pending" : Return Theme.InfoSoft
            Case "preparing" : Return Theme.WarningSoft
            Case "ready" : Return Theme.SuccessSoft
            Case "completed" : Return Color.FromArgb(235, 238, 236)
            Case "cancelled" : Return Theme.DangerSoft
        End Select
        Return Theme.FieldBg
    End Function

    Public Function StatusPill(status As String, Optional prefix As String = "") As PillLabel
        Dim p As New PillLabel()
        p.Text = prefix & Ui.StatusText(status)
        p.FillColor = Ui.StatusBack(status)
        p.TextColor = Ui.StatusFore(status)
        p.FitToText()
        Return p
    End Function

    Public Function OpenPill(isOpen As Boolean) As PillLabel
        Dim p As New PillLabel()
        p.Text = If(isOpen, "Open", "Closed")
        p.FillColor = If(isOpen, Theme.SuccessSoft, Theme.DangerSoft)
        p.TextColor = If(isOpen, Theme.Success, Theme.Danger)
        p.FitToText()
        Return p
    End Function

    ''' <summary>Round orange logo tile with the bowl icon.</summary>
    Public Function LogoTile(size As Integer) As Control
        Dim c As New LogoTileControl()
        c.Size = New Size(size, size)
        Return c
    End Function
End Module

Public Class LogoTileControl
    Inherits Control

    Public Sub New()
        SetStyle(ControlStyles.UserPaint Or ControlStyles.AllPaintingInWmPaint Or ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        SetStyle(ControlStyles.Selectable, False)
    End Sub

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g As Graphics = e.Graphics
        g.Clear(BackColor)
        g.SmoothingMode = SmoothingMode.AntiAlias
        Using path As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, Width - 1, Height - 1), Width \ 3), br As New LinearGradientBrush(New Rectangle(0, 0, Width, Height), Color.FromArgb(255, 140, 70), Theme.Primary, 45.0F)
            g.FillPath(br, path)
        End Using
        Dim s As Integer = CInt(Width * 0.58)
        Icons.Draw(g, "logo", New Rectangle(CInt((Width - s) / 2), CInt((Height - s) / 2), s, s), Color.White)
    End Sub
End Class

''' <summary>Small "added to order" style popup at the bottom of a form.</summary>
Public Module Toast
    Private _current As Control
    Private _timer As Timer

    Public Sub Show(host As Control, message As String, Optional isError As Boolean = False)
        Dim f As Form = host.FindForm()
        If f Is Nothing Then Return
        Hide()
        Dim fnt As Font = Theme.F(10, True)
        Dim tw As Integer = TextRenderer.MeasureText(message, fnt, New Size(600, 50), TextFormatFlags.NoPadding).Width
        Dim p As New Panel()
        p.BackColor = If(isError, Theme.Danger, Color.FromArgb(44, 38, 34))
        p.Size = New Size(tw + 56, 46)
        Dim l As New Label()
        l.Text = message
        l.Font = fnt
        l.ForeColor = Color.White
        l.AutoSize = False
        l.TextAlign = ContentAlignment.MiddleCenter
        l.SetBounds(0, 0, p.Width, p.Height)
        l.UseMnemonic = False
        p.Controls.Add(l)
        Using gp As GraphicsPath = Theme.RoundRectPath(New Rectangle(0, 0, p.Width, p.Height), 14)
            p.Region = New Region(gp)
        End Using
        p.Location = New Point(CInt((f.ClientSize.Width - p.Width) / 2), f.ClientSize.Height - p.Height - 28)
        f.Controls.Add(p)
        p.BringToFront()
        _current = p
        _timer = New Timer()
        _timer.Interval = 2200
        AddHandler _timer.Tick, Sub(s, e) Hide()
        _timer.Start()
    End Sub

    Private Sub Hide()
        If _timer IsNot Nothing Then
            _timer.Stop()
            _timer.Dispose()
            _timer = Nothing
        End If
        If _current IsNot Nothing Then
            If _current.Parent IsNot Nothing Then _current.Parent.Controls.Remove(_current)
            _current.Dispose()
            _current = Nothing
        End If
    End Sub
End Module

''' <summary>Makes the mouse wheel scroll whichever scrollable panel is under the cursor.</summary>
Public Class WheelFilter
    Implements IMessageFilter

    <DllImport("user32.dll")>
    Private Shared Function WindowFromPoint(p As Point) As IntPtr
    End Function

    Private Const WM_MOUSEWHEEL As Integer = &H20A

    Public Function PreFilterMessage(ByRef m As Message) As Boolean Implements IMessageFilter.PreFilterMessage
        If m.Msg <> WM_MOUSEWHEEL Then Return False
        Try
            Dim h As IntPtr = WindowFromPoint(Cursor.Position)
            Dim c As Control = Control.FromHandle(h)
            Dim delta As Integer = CInt((m.WParam.ToInt64() >> 16) And &HFFFFL)
            If delta > 32767 Then delta -= 65536
            While c IsNot Nothing
                Dim pg As PageBase = TryCast(c, PageBase)
                If pg IsNot Nothing Then
                    pg.ScrollByWheel(delta)
                    Return True
                End If
                Dim sc As ScrollableControl = TryCast(c, ScrollableControl)
                If sc IsNot Nothing AndAlso sc.AutoScroll AndAlso sc.VerticalScroll.Visible Then
                    Dim cur As Integer = -sc.AutoScrollPosition.Y
                    Dim nv As Integer = cur - CInt(delta / 120.0 * 80)
                    sc.AutoScrollPosition = New Point(-sc.AutoScrollPosition.X, Math.Max(0, nv))
                    Return True
                End If
                c = c.Parent
            End While
        Catch
        End Try
        Return False
    End Function
End Class
