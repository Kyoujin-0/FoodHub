Option Strict Off
Option Infer On
Imports System.Drawing
Imports System.Windows.Forms
Imports System.Collections.Generic
Imports System.Drawing.Drawing2D
Imports System.Linq
Imports Timer = System.Windows.Forms.Timer

' =====================================================================
' Core/FloorMap3D.vb : the floor map of the admin Stores tab.
' GDI+ "2.5D": every stall is a box projected with a little math (see 3D_Stores_Map_for_WinForms_VB.md).
' Modes:
'   view  : overview (flat) <-> focus on a slot (tilted, rotated so its open side faces you)
'   pick  : like view, vacant slots are highlighted and clicking one raises SlotClicked (used to move a stall)
'   edit  : flat, drag to move, 8 handles to resize, snap to 10, works on the draft layout you give EnterEdit
' The control paints its own rounded card (cream gradient).
' =====================================================================
Public Class FloorMap3D
    Inherits Control

    Public Event SlotClicked(slotNo As Integer)
    Public Event BackClicked()
    Public Event ViewReset()                 ' user went back to the overview (clicked empty space)
    Public Event EditSelectionChanged()
    Public Event EditShapeChanged()

    Public CanvasBack As Color = Color.FromArgb(251, 246, 240)
    Public Const MinSize As Integer = 40
    Public Const GridSize As Integer = 10

    Private ReadOnly Ink As Color = Color.FromArgb(43, 27, 18)
    Private ReadOnly Muted As Color = Color.FromArgb(122, 106, 94)
    Private ReadOnly Accent As Color = Color.FromArgb(255, 107, 53)

    Private _layout As New FloorLayout()
    Private _stalls As New List(Of FloorStall)
    Private _pick As Boolean
    Private _edit As Boolean
    Private _editSel As FloorSlot
    Private selected As Integer = 0          ' focused slot number, 0 = overview

    ' camera
    Private rz As Double = 0
    Private tilt As Double = 0
    Private zoom As Double = 1
    Private lookX As Double
    Private lookY As Double
    Private fromCam(4) As Double
    Private toCam(4) As Double
    Private animStart As DateTime
    Private ReadOnly animMs As Double = 900
    Private WithEvents tmr As New Timer() With {.Interval = 15}

    ' hit testing / overlay
    Private hit As New List(Of KeyValuePair(Of Integer, GraphicsPath))
    Private _backRect As Rectangle = Rectangle.Empty

    ' edit dragging
    Private _dragHandle As Integer = -2      ' -2 none, -1 move, 0..7 handle (nw n ne e se s sw w)
    Private _dragStart As PointF
    Private _orig As Rectangle

    Public Property PickText As String = ""

    Public Sub New()
        SetStyle(ControlStyles.AllPaintingInWmPaint Or ControlStyles.UserPaint Or
                 ControlStyles.OptimizedDoubleBuffer Or ControlStyles.ResizeRedraw, True)
        BackColor = Color.FromArgb(250, 245, 238)
    End Sub

    ' ---------------- properties ----------------

    Public Shadows Property Layout As FloorLayout
        Get
            Return _layout
        End Get
        Set(value As FloorLayout)
            _layout = If(value, New FloorLayout())
            Invalidate()
        End Set
    End Property

    Public Property Stalls As List(Of FloorStall)
        Get
            Return _stalls
        End Get
        Set(value As List(Of FloorStall))
            _stalls = If(value, New List(Of FloorStall))
            Invalidate()
        End Set
    End Property

    Public Property PickMode As Boolean
        Get
            Return _pick
        End Get
        Set(value As Boolean)
            _pick = value
            Invalidate()
        End Set
    End Property

    Public ReadOnly Property EditMode As Boolean
        Get
            Return _edit
        End Get
    End Property

    Public ReadOnly Property SelectedSlot As Integer
        Get
            Return selected
        End Get
    End Property

    Public Property EditSelected As FloorSlot
        Get
            Return _editSel
        End Get
        Set(value As FloorSlot)
            _editSel = value
            Invalidate()
        End Set
    End Property

    ' ---------------- camera API ----------------

    Public Sub ShowOverview(Optional animate As Boolean = True)
        selected = 0
        Dim r As Double = Math.Round(rz / 360) * 360
        If animate AndAlso Width > 0 Then
            MoveCamera(r, 0, FitZoom(), _layout.Width / 2, _layout.Height / 2)
        Else
            tmr.Stop()
            SnapCamera()
        End If
        Invalidate()
    End Sub

    Public Sub FocusSlot(slotNo As Integer)
        Dim s = _layout.FindSlot(slotNo)
        If s Is Nothing Then Return
        selected = slotNo
        Dim want As Double = FaceRotation(s.Face)
        want += 360 * Math.Round((rz - want) / 360)            ' shortest way round
        Dim z As Double = FitZoom() * 2.3
        If Width > 0 Then z = Math.Min(z, Width / (Math.Max(s.W, s.H) * 1.8))
        MoveCamera(want, 66, z, s.X + s.W / 2, s.Y + s.H / 2)
    End Sub

    Public Sub EnterEdit(draft As FloorLayout)
        _layout = draft
        _edit = True
        _pick = False
        selected = 0
        _editSel = Nothing
        _dragHandle = -2
        tmr.Stop()
        SnapCamera()
        Invalidate()
    End Sub

    Public Sub ExitEdit(real As FloorLayout)
        _edit = False
        _editSel = Nothing
        _dragHandle = -2
        _layout = real
        Cursor = Cursors.Default
        ShowOverview(False)
    End Sub

    ''' <summary>Call after the floor size changed in edit mode.</summary>
    Public Sub RefitCamera()
        SnapCamera()
        Invalidate()
    End Sub

    Private Sub SnapCamera()
        rz = 0
        tilt = 0
        zoom = FitZoom()
        lookX = _layout.Width / 2
        lookY = _layout.Height / 2
    End Sub

    Private Shared Function FaceRotation(face As String) As Double
        Select Case face
            Case "e" : Return 90
            Case "w" : Return -90
            Case "n" : Return 180
            Case Else : Return 0
        End Select
    End Function

    Private Function FitZoom() As Double
        If _layout Is Nothing OrElse Width <= 0 OrElse Height <= 0 Then Return 1
        Return Math.Max(0.05, Math.Min((Width - 60.0) / (_layout.Width + 20.0), (Height - 60.0) / (_layout.Height + 20.0)))
    End Function

    Private Sub MoveCamera(r As Double, t As Double, z As Double, lx As Double, ly As Double)
        fromCam = {rz, tilt, zoom, lookX, lookY}
        toCam = {r, t, z, lx, ly}
        animStart = DateTime.Now
        tmr.Start()
    End Sub

    Private Sub tmr_Tick(sender As Object, e As EventArgs) Handles tmr.Tick
        Dim p As Double = Math.Min(1, (DateTime.Now - animStart).TotalMilliseconds / animMs)
        Dim k As Double = If(p < 0.5, 4 * p * p * p, 1 - Math.Pow(-2 * p + 2, 3) / 2)
        rz = fromCam(0) + (toCam(0) - fromCam(0)) * k
        tilt = fromCam(1) + (toCam(1) - fromCam(1)) * k
        zoom = fromCam(2) + (toCam(2) - fromCam(2)) * k
        lookX = fromCam(3) + (toCam(3) - fromCam(3)) * k
        lookY = fromCam(4) + (toCam(4) - fromCam(4)) * k
        If p >= 1 Then tmr.Stop()
        Invalidate()
    End Sub

    Protected Overrides Sub OnResize(e As EventArgs)
        MyBase.OnResize(e)
        If Not tmr.Enabled AndAlso selected = 0 Then SnapCamera()
    End Sub

    Protected Overrides Sub Dispose(disposing As Boolean)
        If disposing Then
            tmr.Stop()
            tmr.Dispose()
            ClearHits()
        End If
        MyBase.Dispose(disposing)
    End Sub

    ' ---------------- projection ----------------

    Private Structure P3
        Public X As Single
        Public Y As Single
        Public Depth As Double
    End Structure

    Private Function Project(x As Double, y As Double, z As Double) As P3
        Dim a As Double = rz * Math.PI / 180, b As Double = tilt * Math.PI / 180
        Dim dx = x - lookX, dy = y - lookY
        Dim x2 = dx * Math.Cos(a) - dy * Math.Sin(a)
        Dim y2 = dx * Math.Sin(a) + dy * Math.Cos(a)
        Dim y3 = y2 * Math.Cos(b) - z * Math.Sin(b)
        Dim d = y2 * Math.Sin(b) + z * Math.Cos(b)
        Dim r As New P3
        r.X = CSng(Width / 2 + zoom * x2)
        r.Y = CSng(Height / 2 + zoom * y3)
        r.Depth = d
        Return r
    End Function

    Private Function Poly(ParamArray pts() As P3) As PointF()
        Dim r(pts.Length - 1) As PointF
        For i = 0 To pts.Length - 1
            r(i) = New PointF(pts(i).X, pts(i).Y)
        Next
        Return r
    End Function

    ''' <summary>Screen rectangle of a floor rectangle (only valid while tilt = 0 and rz = 0, i.e. edit mode).</summary>
    Private Function RectOf(x As Double, y As Double, w As Double, h As Double) As RectangleF
        Dim a = Project(x, y, 0), b = Project(x + w, y + h, 0)
        Return New RectangleF(a.X, a.Y, b.X - a.X, b.Y - a.Y)
    End Function

    Private Function ToWorld(p As Point) As PointF
        Return New PointF(CSng((p.X - Width / 2) / zoom + lookX), CSng((p.Y - Height / 2) / zoom + lookY))
    End Function

    ' ---------------- colours / helpers ----------------

    Private Function StateColor(st As FloorStall) As Color
        Select Case st.RentStatus
            Case "paid" : Return Color.FromArgb(52, 179, 109)
            Case "overdue" : Return Color.FromArgb(220, 84, 70)
            Case Else : Return Color.FromArgb(240, 164, 58)
        End Select
    End Function

    Private Shared Function Mix(c As Color, withColor As Color, amount As Double) As Color
        Return Color.FromArgb(CInt(CInt(c.R) + (CInt(withColor.R) - CInt(c.R)) * amount),
                              CInt(CInt(c.G) + (CInt(withColor.G) - CInt(c.G)) * amount),
                              CInt(CInt(c.B) + (CInt(withColor.B) - CInt(c.B)) * amount))
    End Function

    Private Function FindStall(slotNo As Integer) As FloorStall
        Return _stalls.FirstOrDefault(Function(q) q.Slot = slotNo AndAlso slotNo <> 0)
    End Function

    Private Shared Function RoundPath(r As RectangleF, radius As Single) As GraphicsPath
        Dim p As New GraphicsPath()
        Dim d = Math.Max(1.0F, Math.Min(radius * 2, Math.Min(r.Width, r.Height)))
        p.AddArc(r.X, r.Y, d, d, 180, 90)
        p.AddArc(r.Right - d, r.Y, d, d, 270, 90)
        p.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90)
        p.AddArc(r.X, r.Bottom - d, d, d, 90, 90)
        p.CloseFigure()
        Return p
    End Function

    Private Sub ClearHits()
        For Each kv In hit
            kv.Value.Dispose()
        Next
        hit.Clear()
    End Sub

    Private Sub DrawCentered(g As Graphics, text As String, f As Font, col As Color, cx As Single, cy As Single, w As Single, h As Single)
        Using sf As New StringFormat With {.Alignment = StringAlignment.Center, .LineAlignment = StringAlignment.Center, .Trimming = StringTrimming.EllipsisCharacter}
            Using br As New SolidBrush(col)
                g.DrawString(text, f, br, New RectangleF(cx - w / 2, cy - h / 2, w, h), sf)
            End Using
        End Using
    End Sub

    ' ---------------- painting ----------------

    Protected Overrides Sub OnPaint(e As PaintEventArgs)
        Dim g = e.Graphics
        g.Clear(CanvasBack)
        If Width < 20 OrElse Height < 20 Then Return
        g.SmoothingMode = SmoothingMode.AntiAlias
        ClearHits()
        _backRect = Rectangle.Empty

        Using card = RoundPath(New RectangleF(0, 0, Width - 1, Height - 1), 22)
            Using br As New LinearGradientBrush(New Rectangle(0, 0, Width, Height), Color.FromArgb(253, 248, 242), Color.FromArgb(246, 236, 225), 90.0F)
                g.FillPath(br, card)
            End Using
            g.SetClip(card)
            If _layout IsNot Nothing AndAlso _layout.Slots.Count > 0 Then
                If _edit Then PaintEdit(g) Else PaintView(g)
            End If
            g.ResetClip()
            Using pen As New Pen(Color.FromArgb(240, 230, 218), 1)
                g.DrawPath(pen, card)
            End Using
        End Using
        If Not _edit Then PaintOverlay(g)
    End Sub

    Private Sub PaintView(g As Graphics)
        Dim W = _layout.Width, H = _layout.Height
        Dim floorPts = Poly(Project(0, 0, 0), Project(W, 0, 0), Project(W, H, 0), Project(0, H, 0))
        g.FillPolygon(Brushes.White, floorPts)

        Dim din = _layout.Slots.FirstOrDefault(Function(q) q.IsDining)
        If din IsNot Nothing Then DrawDining(g, din)

        Using pen As New Pen(Color.FromArgb(232, 217, 198), 3)
            g.DrawPolygon(pen, floorPts)
        End Using

        Dim order = _layout.Slots.Where(Function(q) Not q.IsDining).OrderBy(Function(q) Project(q.X + q.W / 2, q.Y + q.H / 2, 0).Depth).ToList()
        For Each s In order
            DrawStall(g, s)
        Next
    End Sub

    Private Sub DrawDining(g As Graphics, d As FloorSlot)
        Dim pts = Poly(Project(d.X, d.Y, 0), Project(d.X + d.W, d.Y, 0), Project(d.X + d.W, d.Y + d.H, 0), Project(d.X, d.Y + d.H, 0))
        Using br As New SolidBrush(Color.FromArgb(253, 241, 234))
            g.FillPolygon(br, pts)
        End Using
        Using pen As New Pen(Color.FromArgb(244, 228, 218), 1)
            Dim gx As Integer = d.X + 40
            Do While gx < d.X + d.W
                Dim a = Project(gx, d.Y, 0), b = Project(gx, d.Y + d.H, 0)
                g.DrawLine(pen, a.X, a.Y, b.X, b.Y)
                gx += 40
            Loop
            Dim gy As Integer = d.Y + 40
            Do While gy < d.Y + d.H
                Dim a = Project(d.X, gy, 0), b = Project(d.X + d.W, gy, 0)
                g.DrawLine(pen, a.X, a.Y, b.X, b.Y)
                gy += 40
            Loop
        End Using
        Using pen As New Pen(Color.FromArgb(236, 214, 200), 2)
            pen.DashStyle = DashStyle.Dash
            g.DrawPolygon(pen, pts)
        End Using
        If tilt < 20 Then
            Dim c = Project(d.X + d.W / 2, d.Y + d.H / 2, 0)
            DrawCentered(g, "DINING AREA", Theme.F(20.0F, True), Color.FromArgb(226, 210, 196), c.X, c.Y, CSng(d.W * zoom), 50)
        End If
    End Sub

    Private Sub DrawStall(g As Graphics, s As FloorSlot)
        Dim st = FindStall(s.Slot)
        Dim vacant As Boolean = (st Is Nothing)
        Dim baseCol As Color = If(vacant, Color.FromArgb(216, 205, 191), StateColor(st))
        Dim dimmed As Boolean = (selected <> 0 AndAlso selected <> s.Slot) OrElse (_pick AndAlso Not vacant)
        Dim h As Double = If(vacant, 4.0, 38.0)
        If tilt < 1 Then h = 0

        Dim x0 As Double = s.X, y0 As Double = s.Y, x1 As Double = s.X + s.W, y1 As Double = s.Y + s.H
        Dim a As Double = rz * Math.PI / 180, b As Double = tilt * Math.PI / 180

        ' walls: north, east, south, west
        Dim ax() As Double = {x0, x1, x0, x0}
        Dim ay() As Double = {y0, y0, y1, y0}
        Dim bx() As Double = {x1, x1, x1, x0}
        Dim byy() As Double = {y0, y1, y1, y1}
        Dim nx() As Double = {0, 1, 0, -1}
        Dim ny() As Double = {-1, 0, 1, 0}

        Dim bestScore As Double = 0.0
        Dim bestQuad As PointF() = Nothing
        If h > 0 Then
            For i = 0 To 3
                Dim ny2 = nx(i) * Math.Sin(a) + ny(i) * Math.Cos(a)
                Dim score = ny2 * Math.Sin(b)
                If score <= 0 Then Continue For
                Dim quad = Poly(Project(ax(i), ay(i), 0), Project(bx(i), byy(i), 0), Project(bx(i), byy(i), h), Project(ax(i), ay(i), h))
                Dim wallCol As Color = If(vacant, Color.FromArgb(235, 228, 219), Mix(baseCol, Color.White, If(dimmed, 0.7, 0.42)))
                Using br As New SolidBrush(Color.FromArgb(205, wallCol))
                    g.FillPolygon(br, quad)
                End Using
                Using pen As New Pen(Mix(baseCol, Color.Black, 0.3), 1.5F)
                    g.DrawPolygon(pen, quad)
                End Using
                If score > bestScore Then
                    bestScore = score
                    bestQuad = quad
                End If
            Next
        End If

        ' roof
        Dim roof = Poly(Project(x0, y0, h), Project(x1, y0, h), Project(x1, y1, h), Project(x0, y1, h))
        Dim roofCol As Color
        If vacant Then
            roofCol = If(_pick, Color.FromArgb(255, 231, 218), Color.FromArgb(253, 251, 248))
        Else
            roofCol = Mix(baseCol, Color.White, If(dimmed, 0.62, If(s.Slot = selected, 0.28, 0.2)))
        End If
        Using br As New SolidBrush(roofCol)
            g.FillPolygon(br, roof)
        End Using
        If st IsNot Nothing AndAlso Not st.IsOpen Then
            Using hb As New HatchBrush(HatchStyle.WideUpwardDiagonal, Color.FromArgb(70, 0, 0, 0), Color.Transparent)
                g.FillPolygon(hb, roof)
            End Using
        End If

        Dim outlineCol As Color = If(s.Slot = selected AndAlso selected <> 0, Ink, Mix(baseCol, Color.Black, 0.3))
        Dim outlineW As Single = If(s.Slot = selected AndAlso selected <> 0, 3.0F, 1.5F)
        If vacant Then
            outlineCol = If(_pick, Accent, Color.FromArgb(200, 188, 176))
            outlineW = If(_pick, 2.5F, 1.5F)
        End If
        Using pen As New Pen(outlineCol, outlineW)
            If vacant Then pen.DashStyle = DashStyle.Dash
            g.DrawPolygon(pen, roof)
        End Using

        ' text
        Dim sw As Single = CSng(If(Math.Abs(Math.Sin(a)) > 0.7, s.H, s.W) * zoom * 0.9)
        If tmr.Enabled Then
            ' no stall names while the camera is moving
        ElseIf tilt < 20 Then
            Dim c = Project((x0 + x1) / 2, (y0 + y1) / 2, h)
            If vacant Then
                DrawCentered(g, "+ Vacant", Theme.F(9.5F, True), Muted, c.X, c.Y - 7, sw, 18)
                DrawCentered(g, "Slot " & s.Slot, Theme.F(8.0F, False), Muted, c.X, c.Y + 8, sw, 14)
            Else
                DrawCentered(g, st.Name, Theme.F(9.5F, True), Ink, c.X, c.Y - If(st.IsOpen, 0, 6), sw, 40)
                If Not st.IsOpen Then DrawCentered(g, "Closed", Theme.F(8.0F, False), Muted, c.X, c.Y + 12, sw, 14)
            End If
        ElseIf bestQuad IsNot Nothing AndAlso Not vacant Then
            Dim cx = (bestQuad(0).X + bestQuad(1).X + bestQuad(2).X + bestQuad(3).X) / 4
            Dim cy = (bestQuad(0).Y + bestQuad(1).Y + bestQuad(2).Y + bestQuad(3).Y) / 4
            Dim wallW = CSng(Math.Sqrt((bestQuad(1).X - bestQuad(0).X) ^ 2 + (bestQuad(1).Y - bestQuad(0).Y) ^ 2))
            If wallW > 60 Then DrawCentered(g, st.Name, Theme.F(10.5F, True), Ink, cx, cy, wallW - 10, 30)
        End If

        Dim path As New GraphicsPath()
        path.AddPolygon(roof)
        hit.Add(New KeyValuePair(Of Integer, GraphicsPath)(s.Slot, path))
    End Sub

    Private Sub PaintOverlay(g As Graphics)
        If selected <> 0 OrElse _pick Then
            Dim txt As String = If(_pick, "Cancel", ChrW(8592) & "  Overview")
            Dim f = Theme.F(10.5F, True)
            Dim sz = TextRenderer.MeasureText(txt, f)
            _backRect = New Rectangle(22, 22, sz.Width + 34, 44)
            Using p = RoundPath(New RectangleF(_backRect.X, _backRect.Y, _backRect.Width, _backRect.Height), 22)
                Using sh As New SolidBrush(Color.FromArgb(18, 0, 0, 0))
                    g.TranslateTransform(0, 2)
                    g.FillPath(sh, p)
                    g.ResetTransform()
                End Using
                g.FillPath(Brushes.White, p)
                Using pen As New Pen(Color.FromArgb(236, 226, 214), 1)
                    g.DrawPath(pen, p)
                End Using
            End Using
            TextRenderer.DrawText(g, txt, f, _backRect, Ink, TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
        End If
        Dim hint As String = ""
        If _pick Then
            hint = PickText
        ElseIf selected = 0 Then
            hint = "Select a stall to take a closer look"
        End If
        If hint <> "" Then
            TextRenderer.DrawText(g, hint, Theme.F(11.5F, False), New Rectangle(0, Height - 46, Width, 30), Muted,
                                  TextFormatFlags.HorizontalCenter Or TextFormatFlags.VerticalCenter Or TextFormatFlags.SingleLine)
        End If
    End Sub

    ' ---------------- edit mode painting ----------------

    Private Function Overlaps(s As FloorSlot) As Boolean
        If s.IsDining Then Return False
        For Each q In _layout.Slots
            If q IsNot s AndAlso Not q.IsDining AndAlso FloorSvc.Overlap(s, q) Then Return True
        Next
        Return False
    End Function

    Private Sub PaintEdit(g As Graphics)
        Dim W = _layout.Width, H = _layout.Height
        Dim fr = RectOf(0, 0, W, H)
        g.FillRectangle(Brushes.White, fr)
        Using pen As New Pen(Color.FromArgb(246, 238, 229), 1)
            For gx = 0 To W Step 50
                Dim a = Project(gx, 0, 0), b = Project(gx, H, 0)
                g.DrawLine(pen, a.X, a.Y, b.X, b.Y)
            Next
            For gy = 0 To H Step 50
                Dim a = Project(0, gy, 0), b = Project(W, gy, 0)
                g.DrawLine(pen, a.X, a.Y, b.X, b.Y)
            Next
        End Using
        Using pen As New Pen(Color.FromArgb(232, 217, 198), 3)
            g.DrawRectangle(pen, fr.X, fr.Y, fr.Width, fr.Height)
        End Using

        ' dining first, then stalls
        For Each s In _layout.Slots.Where(Function(q) q.IsDining)
            Dim r = RectOf(s.X, s.Y, s.W, s.H)
            Using br As New SolidBrush(Color.FromArgb(253, 241, 234))
                g.FillRectangle(br, r)
            End Using
            Using pen As New Pen(Color.FromArgb(230, 205, 190), 2)
                pen.DashStyle = DashStyle.Dash
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height)
            End Using
            DrawCentered(g, "DINING AREA", Theme.F(16.0F, True), Color.FromArgb(222, 205, 190), r.X + r.Width / 2, r.Y + r.Height / 2, r.Width, 40)
        Next
        For Each s In _layout.Slots.Where(Function(q) Not q.IsDining)
            DrawEditSlot(g, s)
        Next
        If _editSel IsNot Nothing Then DrawHandles(g, _editSel)
    End Sub

    Private Sub DrawEditSlot(g As Graphics, s As FloorSlot)
        Dim r = RectOf(s.X, s.Y, s.W, s.H)
        Dim st = FindStall(s.Slot)
        Dim bad = Overlaps(s)
        Dim fill As Color = If(st Is Nothing, Color.FromArgb(246, 240, 232), Mix(StateColor(st), Color.White, 0.45))
        If bad Then fill = Color.FromArgb(250, 205, 200)
        Using br As New SolidBrush(fill)
            g.FillRectangle(br, r)
        End Using
        Using pen As New Pen(If(bad, Color.FromArgb(220, 84, 70), Color.FromArgb(190, 175, 160)), If(bad, 2.5F, 1.5F))
            If st Is Nothing AndAlso Not bad Then pen.DashStyle = DashStyle.Dash
            g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height)
        End Using
        ' the open side
        Using pen As New Pen(Accent, 4)
            Select Case s.Face
                Case "n" : g.DrawLine(pen, r.X + 3, r.Y, r.Right - 3, r.Y)
                Case "e" : g.DrawLine(pen, r.Right, r.Y + 3, r.Right, r.Bottom - 3)
                Case "w" : g.DrawLine(pen, r.X, r.Y + 3, r.X, r.Bottom - 3)
                Case Else : g.DrawLine(pen, r.X + 3, r.Bottom, r.Right - 3, r.Bottom)
            End Select
        End Using
        Dim label As String = "Slot " & s.Slot
        If st IsNot Nothing Then label = st.Name & vbLf & "Slot " & s.Slot
        DrawCentered(g, label, Theme.F(9.0F, True), Ink, r.X + r.Width / 2, r.Y + r.Height / 2, r.Width - 8, r.Height - 6)
        If s Is _editSel Then
            Using pen As New Pen(Accent, 3)
                g.DrawRectangle(pen, r.X, r.Y, r.Width, r.Height)
            End Using
        End If
    End Sub

    Private Function HandlePoints(r As RectangleF) As PointF()
        Dim mx = r.X + r.Width / 2, my = r.Y + r.Height / 2
        Return {New PointF(r.X, r.Y), New PointF(mx, r.Y), New PointF(r.Right, r.Y), New PointF(r.Right, my),
                New PointF(r.Right, r.Bottom), New PointF(mx, r.Bottom), New PointF(r.X, r.Bottom), New PointF(r.X, my)}
    End Function

    Private Sub DrawHandles(g As Graphics, s As FloorSlot)
        Dim r = RectOf(s.X, s.Y, s.W, s.H)
        For Each hpt In HandlePoints(r)
            Dim hr As New RectangleF(hpt.X - 5, hpt.Y - 5, 10, 10)
            g.FillRectangle(Brushes.White, hr)
            Using pen As New Pen(Accent, 2)
                g.DrawRectangle(pen, hr.X, hr.Y, hr.Width, hr.Height)
            End Using
        Next
    End Sub

    ' ---------------- mouse ----------------

    Protected Overrides Sub OnMouseClick(e As MouseEventArgs)
        MyBase.OnMouseClick(e)
        If _edit Then Return
        If _backRect <> Rectangle.Empty AndAlso _backRect.Contains(e.Location) Then
            RaiseEvent BackClicked()
            Return
        End If
        For i = hit.Count - 1 To 0 Step -1                  ' last drawn = nearest
            If hit(i).Value.IsVisible(e.Location) Then
                RaiseEvent SlotClicked(hit(i).Key)
                Return
            End If
        Next
        If selected <> 0 AndAlso Not _pick Then
            ShowOverview()
            RaiseEvent ViewReset()
        End If
    End Sub

    Private Function HandleAt(p As Point) As Integer
        If _editSel Is Nothing Then Return -1
        Dim pts = HandlePoints(RectOf(_editSel.X, _editSel.Y, _editSel.W, _editSel.H))
        For i = 0 To pts.Length - 1
            If Math.Abs(p.X - pts(i).X) <= 8 AndAlso Math.Abs(p.Y - pts(i).Y) <= 8 Then Return i
        Next
        Return -1
    End Function

    Private Function ShapeAt(p As Point) As FloorSlot
        For Each s In _layout.Slots.Where(Function(q) Not q.IsDining).Reverse()
            If RectOf(s.X, s.Y, s.W, s.H).Contains(p) Then Return s
        Next
        For Each s In _layout.Slots.Where(Function(q) q.IsDining)
            If RectOf(s.X, s.Y, s.W, s.H).Contains(p) Then Return s
        Next
        Return Nothing
    End Function

    Private Shared Function HasLeft(h As Integer) As Boolean
        Return h = 0 OrElse h = 6 OrElse h = 7
    End Function
    Private Shared Function HasRight(h As Integer) As Boolean
        Return h = 2 OrElse h = 3 OrElse h = 4
    End Function
    Private Shared Function HasTop(h As Integer) As Boolean
        Return h = 0 OrElse h = 1 OrElse h = 2
    End Function
    Private Shared Function HasBottom(h As Integer) As Boolean
        Return h = 4 OrElse h = 5 OrElse h = 6
    End Function

    Private Shared Function Snap(v As Double) As Integer
        Return CInt(Math.Round(v / GridSize)) * GridSize
    End Function

    Protected Overrides Sub OnMouseDown(e As MouseEventArgs)
        MyBase.OnMouseDown(e)
        If Not _edit OrElse e.Button <> MouseButtons.Left Then Return
        Focus()
        Dim hd = HandleAt(e.Location)
        If hd >= 0 Then
            BeginDrag(hd, e.Location)
            Return
        End If
        Dim picked = ShapeAt(e.Location)
        If picked IsNot _editSel Then
            _editSel = picked
            RaiseEvent EditSelectionChanged()
            Invalidate()
        End If
        If picked IsNot Nothing Then BeginDrag(-1, e.Location)
    End Sub

    Private Sub BeginDrag(handle As Integer, p As Point)
        _dragHandle = handle
        _dragStart = ToWorld(p)
        _orig = New Rectangle(_editSel.X, _editSel.Y, _editSel.W, _editSel.H)
    End Sub

    Protected Overrides Sub OnMouseMove(e As MouseEventArgs)
        MyBase.OnMouseMove(e)
        If _edit Then
            If _dragHandle <> -2 AndAlso _editSel IsNot Nothing Then
                DragTo(e.Location)
            Else
                Dim hd = HandleAt(e.Location)
                If hd >= 0 Then
                    Select Case hd
                        Case 0, 4 : Cursor = Cursors.SizeNWSE
                        Case 2, 6 : Cursor = Cursors.SizeNESW
                        Case 1, 5 : Cursor = Cursors.SizeNS
                        Case Else : Cursor = Cursors.SizeWE
                    End Select
                ElseIf ShapeAt(e.Location) IsNot Nothing Then
                    Cursor = Cursors.SizeAll
                Else
                    Cursor = Cursors.Default
                End If
            End If
        Else
            Dim over As Boolean = (_backRect <> Rectangle.Empty AndAlso _backRect.Contains(e.Location))
            If Not over Then
                For Each kv In hit
                    If kv.Value.IsVisible(e.Location) Then
                        over = True
                        Exit For
                    End If
                Next
            End If
            Cursor = If(over, Cursors.Hand, Cursors.Default)
        End If
    End Sub

    Private Sub DragTo(p As Point)
        Dim w = ToWorld(p)
        Dim dx = Snap(w.X - _dragStart.X), dy = Snap(w.Y - _dragStart.Y)
        Dim s = _editSel
        If _dragHandle = -1 Then
            s.X = Math.Max(0, Math.Min(_orig.X + dx, _layout.Width - _orig.Width))
            s.Y = Math.Max(0, Math.Min(_orig.Y + dy, _layout.Height - _orig.Height))
        Else
            Dim x1 = _orig.Left, y1 = _orig.Top, x2 = _orig.Right, y2 = _orig.Bottom
            If HasLeft(_dragHandle) Then x1 = Math.Max(0, Math.Min(_orig.Left + dx, x2 - MinSize))
            If HasRight(_dragHandle) Then x2 = Math.Min(_layout.Width, Math.Max(_orig.Right + dx, x1 + MinSize))
            If HasTop(_dragHandle) Then y1 = Math.Max(0, Math.Min(_orig.Top + dy, y2 - MinSize))
            If HasBottom(_dragHandle) Then y2 = Math.Min(_layout.Height, Math.Max(_orig.Bottom + dy, y1 + MinSize))
            s.X = x1 : s.Y = y1 : s.W = x2 - x1 : s.H = y2 - y1
        End If
        RaiseEvent EditShapeChanged()
        Invalidate()
    End Sub

    Protected Overrides Sub OnMouseUp(e As MouseEventArgs)
        MyBase.OnMouseUp(e)
        _dragHandle = -2
    End Sub
End Class
