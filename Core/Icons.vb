Imports System.Drawing
Imports System.Drawing.Drawing2D

''' <summary>Vector icons drawn with GDI+ (no image files or icon fonts needed).</summary>
Public Module Icons

    Private Function Pt(r As Rectangle, fx As Single, fy As Single) As PointF
        Return New PointF(r.X + r.Width * fx, r.Y + r.Height * fy)
    End Function

    Public Sub Draw(g As Graphics, name As String, r As Rectangle, c As Color, Optional filled As Boolean = False)
        Dim oldMode As SmoothingMode = g.SmoothingMode
        g.SmoothingMode = SmoothingMode.AntiAlias
        Dim w As Single = Math.Max(1.6F, r.Width / 11.0F)
        Using pen As New Pen(c, w), br As New SolidBrush(c)
            pen.StartCap = LineCap.Round
            pen.EndCap = LineCap.Round
            pen.LineJoin = LineJoin.Round
            Select Case name
                Case "eye", "eyeoff"
                    Dim x0 As Single = r.X + r.Width * 0.04F
                    Dim x1 As Single = r.Right - r.Width * 0.04F
                    Dim cy As Single = r.Y + r.Height * 0.5F
                    Dim hh As Single = r.Height * 0.32F
                    Using gp As New GraphicsPath()
                        gp.AddBezier(x0, cy, x0 + r.Width * 0.25F, cy - hh * 1.6F, x1 - r.Width * 0.25F, cy - hh * 1.6F, x1, cy)
                        gp.AddBezier(x1, cy, x1 - r.Width * 0.25F, cy + hh * 1.6F, x0 + r.Width * 0.25F, cy + hh * 1.6F, x0, cy)
                        g.DrawPath(pen, gp)
                    End Using
                    Dim pr As Single = r.Height * 0.17F
                    g.FillEllipse(br, r.X + r.Width * 0.5F - pr, cy - pr, pr * 2, pr * 2)
                    If name = "eyeoff" Then
                        g.DrawLine(pen, Pt(r, 0.14F, 0.88F), Pt(r, 0.86F, 0.12F))
                    End If
                Case "search"
                    g.DrawEllipse(pen, r.X + r.Width * 0.08F, r.Y + r.Height * 0.08F, r.Width * 0.6F, r.Height * 0.6F)
                    g.DrawLine(pen, Pt(r, 0.62F, 0.62F), Pt(r, 0.92F, 0.92F))
                Case "cart"
                    Dim pts As PointF() = {Pt(r, 0.04F, 0.1F), Pt(r, 0.2F, 0.1F), Pt(r, 0.34F, 0.62F), Pt(r, 0.82F, 0.62F), Pt(r, 0.94F, 0.26F), Pt(r, 0.24F, 0.26F)}
                    g.DrawLines(pen, pts)
                    g.FillEllipse(br, r.X + r.Width * 0.3F, r.Y + r.Height * 0.76F, r.Width * 0.16F, r.Height * 0.16F)
                    g.FillEllipse(br, r.X + r.Width * 0.7F, r.Y + r.Height * 0.76F, r.Width * 0.16F, r.Height * 0.16F)
                Case "user"
                    g.DrawEllipse(pen, r.X + r.Width * 0.3F, r.Y + r.Height * 0.08F, r.Width * 0.4F, r.Height * 0.4F)
                    g.DrawArc(pen, r.X + r.Width * 0.1F, r.Y + r.Height * 0.55F, r.Width * 0.8F, r.Height * 0.8F, 195, 150)
                Case "heart"
                    Dim pts As New List(Of PointF)
                    For i As Integer = 0 To 60
                        Dim t As Double = i / 60.0 * 2 * Math.PI
                        Dim x As Double = 16 * Math.Pow(Math.Sin(t), 3)
                        Dim y As Double = 13 * Math.Cos(t) - 5 * Math.Cos(2 * t) - 2 * Math.Cos(3 * t) - Math.Cos(4 * t)
                        pts.Add(New PointF(CSng(r.X + r.Width * (0.5 + x / 36.0)), CSng(r.Y + r.Height * (0.46 - y / 36.0))))
                    Next
                    If filled Then g.FillPolygon(br, pts.ToArray())
                    g.DrawPolygon(pen, pts.ToArray())
                Case "trash"
                    g.DrawLine(pen, Pt(r, 0.14F, 0.22F), Pt(r, 0.86F, 0.22F))
                    g.DrawLine(pen, Pt(r, 0.38F, 0.22F), Pt(r, 0.38F, 0.1F))
                    g.DrawLine(pen, Pt(r, 0.38F, 0.1F), Pt(r, 0.62F, 0.1F))
                    g.DrawLine(pen, Pt(r, 0.62F, 0.1F), Pt(r, 0.62F, 0.22F))
                    Dim body As PointF() = {Pt(r, 0.22F, 0.3F), Pt(r, 0.28F, 0.92F), Pt(r, 0.72F, 0.92F), Pt(r, 0.78F, 0.3F)}
                    g.DrawLines(pen, body)
                    g.DrawLine(pen, Pt(r, 0.43F, 0.42F), Pt(r, 0.43F, 0.8F))
                    g.DrawLine(pen, Pt(r, 0.57F, 0.42F), Pt(r, 0.57F, 0.8F))
                Case "bookmark"
                    Dim pts As PointF() = {Pt(r, 0.22F, 0.06F), Pt(r, 0.78F, 0.06F), Pt(r, 0.78F, 0.94F), Pt(r, 0.5F, 0.72F), Pt(r, 0.22F, 0.94F)}
                    If filled Then g.FillPolygon(br, pts)
                    g.DrawPolygon(pen, pts)
                Case "back", "chev_l"
                    g.DrawLines(pen, New PointF() {Pt(r, 0.62F, 0.15F), Pt(r, 0.3F, 0.5F), Pt(r, 0.62F, 0.85F)})
                Case "chev_r"
                    g.DrawLines(pen, New PointF() {Pt(r, 0.38F, 0.15F), Pt(r, 0.7F, 0.5F), Pt(r, 0.38F, 0.85F)})
                Case "chev_d"
                    g.DrawLines(pen, New PointF() {Pt(r, 0.15F, 0.38F), Pt(r, 0.5F, 0.7F), Pt(r, 0.85F, 0.38F)})
                Case "x"
                    g.DrawLine(pen, Pt(r, 0.2F, 0.2F), Pt(r, 0.8F, 0.8F))
                    g.DrawLine(pen, Pt(r, 0.8F, 0.2F), Pt(r, 0.2F, 0.8F))
                Case "plus"
                    g.DrawLine(pen, Pt(r, 0.2F, 0.5F), Pt(r, 0.8F, 0.5F))
                    g.DrawLine(pen, Pt(r, 0.5F, 0.2F), Pt(r, 0.5F, 0.8F))
                Case "minus"
                    g.DrawLine(pen, Pt(r, 0.2F, 0.5F), Pt(r, 0.8F, 0.5F))
                Case "check"
                    g.DrawLines(pen, New PointF() {Pt(r, 0.18F, 0.52F), Pt(r, 0.42F, 0.76F), Pt(r, 0.84F, 0.26F)})
                Case "star"
                    Dim pts As New List(Of PointF)
                    For i As Integer = 0 To 9
                        Dim rad As Double = If(i Mod 2 = 0, 0.5, 0.21)
                        Dim ang As Double = -Math.PI / 2 + i * Math.PI / 5
                        pts.Add(New PointF(CSng(r.X + r.Width * (0.5 + rad * Math.Cos(ang))), CSng(r.Y + r.Height * (0.55 + rad * Math.Sin(ang)))))
                    Next
                    g.FillPolygon(br, pts.ToArray())
                Case "home"
                    g.DrawLines(pen, New PointF() {Pt(r, 0.1F, 0.5F), Pt(r, 0.5F, 0.12F), Pt(r, 0.9F, 0.5F)})
                    g.DrawLines(pen, New PointF() {Pt(r, 0.22F, 0.42F), Pt(r, 0.22F, 0.88F), Pt(r, 0.78F, 0.88F), Pt(r, 0.78F, 0.42F)})
                Case "message"
                    Using gp As GraphicsPath = Theme.RoundRectPath(New Rectangle(r.X + CInt(r.Width * 0.06F), r.Y + CInt(r.Height * 0.1F), CInt(r.Width * 0.88F), CInt(r.Height * 0.62F)), CInt(r.Width * 0.14F))
                        g.DrawPath(pen, gp)
                    End Using
                    g.DrawLines(pen, New PointF() {Pt(r, 0.3F, 0.72F), Pt(r, 0.26F, 0.92F), Pt(r, 0.5F, 0.72F)})
                Case "receipt"
                    g.DrawLines(pen, New PointF() {Pt(r, 0.2F, 0.08F), Pt(r, 0.2F, 0.92F), Pt(r, 0.32F, 0.82F), Pt(r, 0.44F, 0.92F), Pt(r, 0.56F, 0.82F), Pt(r, 0.68F, 0.92F), Pt(r, 0.8F, 0.82F), Pt(r, 0.8F, 0.08F), Pt(r, 0.2F, 0.08F)})
                    g.DrawLine(pen, Pt(r, 0.34F, 0.3F), Pt(r, 0.66F, 0.3F))
                    g.DrawLine(pen, Pt(r, 0.34F, 0.5F), Pt(r, 0.66F, 0.5F))
                Case "logo"
                    ' a steaming bowl
                    g.DrawArc(pen, r.X + r.Width * 0.12F, r.Y + r.Height * 0.3F, r.Width * 0.76F, r.Height * 0.62F, 0, 180)
                    g.DrawLine(pen, Pt(r, 0.1F, 0.61F), Pt(r, 0.9F, 0.61F))
                    g.DrawLine(pen, Pt(r, 0.34F, 0.9F), Pt(r, 0.66F, 0.9F))
                    g.DrawArc(pen, r.X + r.Width * 0.3F, r.Y + r.Height * 0.04F, r.Width * 0.12F, r.Height * 0.22F, 90, 180)
                    g.DrawArc(pen, r.X + r.Width * 0.5F, r.Y + r.Height * 0.04F, r.Width * 0.12F, r.Height * 0.22F, 90, 180)
            End Select
        End Using
        g.SmoothingMode = oldMode
    End Sub
End Module
