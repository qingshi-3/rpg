Add-Type -AssemblyName System.Drawing
$outDir = Join-Path (Get-Location) 'output\mockups\site-management-v1'
New-Item -ItemType Directory -Force -Path $outDir | Out-Null
$privateFonts = New-Object System.Drawing.Text.PrivateFontCollection
$privateFonts.AddFontFile('C:\Windows\Fonts\msyh.ttc')
$fontFamily = $privateFonts.Families[0]
function Font($size, $style = [System.Drawing.FontStyle]::Regular) { New-Object System.Drawing.Font($fontFamily, $size, $style, [System.Drawing.GraphicsUnit]::Pixel) }
function Brush($hex) { New-Object System.Drawing.SolidBrush([System.Drawing.ColorTranslator]::FromHtml($hex)) }
function PenC($hex, $w=1) { New-Object System.Drawing.Pen([System.Drawing.ColorTranslator]::FromHtml($hex), $w) }
function Rect($x,$y,$w,$h) { New-Object System.Drawing.RectangleF([float]$x,[float]$y,[float]$w,[float]$h) }
function Pt($x,$y) { New-Object System.Drawing.PointF([float]$x,[float]$y) }
function RoundRectPath($x,$y,$w,$h,$r) { $path=New-Object System.Drawing.Drawing2D.GraphicsPath; $d=$r*2; $path.AddArc($x,$y,$d,$d,180,90); $path.AddArc($x+$w-$d,$y,$d,$d,270,90); $path.AddArc($x+$w-$d,$y+$h-$d,$d,$d,0,90); $path.AddArc($x,$y+$h-$d,$d,$d,90,90); $path.CloseFigure(); $path }
function FillRound($g,$x,$y,$w,$h,$r,$fill,$stroke=$null,$sw=1) { $path=RoundRectPath $x $y $w $h $r; $b=Brush $fill; $g.FillPath($b,$path); $b.Dispose(); if($stroke){$p=PenC $stroke $sw; $g.DrawPath($p,$path); $p.Dispose()}; $path.Dispose() }
function Text($g,$s,$x,$y,$size,$color='#EDE6D2',$style=[System.Drawing.FontStyle]::Regular,$w=0,$h=0) { $f=Font $size $style; $b=Brush $color; if($w -gt 0){ $g.DrawString($s,$f,$b,(Rect $x $y $w $h)) } else { $g.DrawString($s,$f,$b,(Pt $x $y)) }; $b.Dispose(); $f.Dispose() }
function CenterText($g,$s,$x,$y,$w,$h,$size,$color='#EDE6D2',$style=[System.Drawing.FontStyle]::Regular) { $f=Font $size $style; $b=Brush $color; $sf=New-Object System.Drawing.StringFormat; $sf.Alignment=[System.Drawing.StringAlignment]::Center; $sf.LineAlignment=[System.Drawing.StringAlignment]::Center; $g.DrawString($s,$f,$b,(Rect $x $y $w $h),$sf); $sf.Dispose(); $b.Dispose(); $f.Dispose() }
function Line($g,$x1,$y1,$x2,$y2,$color='#D6B46A',$w=2) { $p=PenC $color $w; $g.DrawLine($p,[float]$x1,[float]$y1,[float]$x2,[float]$y2); $p.Dispose() }
function Arrow($g,$x1,$y1,$x2,$y2,$color='#D6B46A') { $p=PenC $color 3; $cap=New-Object System.Drawing.Drawing2D.AdjustableArrowCap(5,7); $p.CustomEndCap=$cap; $g.DrawLine($p,[float]$x1,[float]$y1,[float]$x2,[float]$y2); $cap.Dispose(); $p.Dispose() }
function Canvas($w,$h,$title,$subtitle) { $bmp=New-Object System.Drawing.Bitmap $w,$h; $g=[System.Drawing.Graphics]::FromImage($bmp); $g.SmoothingMode=[System.Drawing.Drawing2D.SmoothingMode]::AntiAlias; $g.TextRenderingHint=[System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit; $g.Clear([System.Drawing.ColorTranslator]::FromHtml('#101113')); $bg=New-Object System.Drawing.Drawing2D.LinearGradientBrush((Rect 0 0 $w $h),[System.Drawing.ColorTranslator]::FromHtml('#11151A'),[System.Drawing.ColorTranslator]::FromHtml('#272014'),45); $g.FillRectangle($bg,0,0,$w,$h); $bg.Dispose(); Text $g $title 34 24 32 '#F5E5B7' ([System.Drawing.FontStyle]::Bold); Text $g $subtitle 36 65 17 '#B9AA83'; @($bmp,$g) }
function DrawSlot($g,$x,$y,$label,$state,$accent,$sub,$progress=-1) { FillRound $g $x $y 160 110 14 '#1C1D1B' $accent 3; $p=PenC '#6E5530' 2; $g.DrawEllipse($p,$x+18,$y+20,124,50); $p.Dispose(); if($state -eq '空槽'){ $p=PenC '#9A7B45' 2; $p.DashStyle=[System.Drawing.Drawing2D.DashStyle]::Dash; $g.DrawRectangle($p,$x+45,$y+20,70,48); $p.Dispose(); CenterText $g '+' ($x+45) ($y+20) 70 48 34 '#BFA66A' } elseif($state -eq '施工中'){ FillRound $g ($x+45) ($y+24) 70 42 8 '#6A5130' '#D6B46A' 2; Line $g ($x+55) ($y+23) ($x+105) ($y+66) '#E1C27D' 3; Line $g ($x+105) ($y+23) ($x+55) ($y+66) '#E1C27D' 3 } elseif($state -eq '完成'){ FillRound $g ($x+42) ($y+18) 76 50 8 '#4B5B40' '#A7D37B' 2; FillRound $g ($x+55) ($y+4) 50 28 6 '#34462E' '#A7D37B' 2 } else { FillRound $g ($x+42) ($y+18) 76 50 8 '#3B332E' '#D0694B' 2; Line $g ($x+50) ($y+22) ($x+72) ($y+64) '#D0694B' 3; Line $g ($x+98) ($y+20) ($x+83) ($y+65) '#D0694B' 3 }; Text $g $label ($x+14) ($y+72) 18 '#F4E9C9' ([System.Drawing.FontStyle]::Bold); Text $g $sub ($x+14) ($y+94) 14 '#BEB292'; if($progress -ge 0){ FillRound $g ($x+14) ($y+88) 132 8 4 '#2B2B2B'; FillRound $g ($x+14) ($y+88) (132*$progress) 8 4 $accent } }

# 01 overview
$pair=Canvas 1600 1000 '场域经营 V1：地图表面经营 + 场域内推进世界步' '玩家进入场域后能直接在地图上经营、观察施工与产出；战斗回合与世界步严格隔离。'
$bmp=$pair[0]; $g=$pair[1]
FillRound $g 32 105 1536 70 18 '#1B1C20' '#5A4B2D' 2
Text $g '埋骨地 Bonefield' 58 126 28 '#F4E9C9' ([System.Drawing.FontStyle]::Bold)
Text $g '世界第 12 日｜暂停规划｜人口 5｜经济 8｜石材 12' 330 132 20 '#D8C79C'
FillRound $g 1020 122 120 36 10 '#2D3D2E' '#89C16A' 2; CenterText $g '推进一日' 1020 122 120 36 18 '#ECF8DD' ([System.Drawing.FontStyle]::Bold)
FillRound $g 1158 122 120 36 10 '#263044' '#72A3D8' 2; CenterText $g '正常速度' 1158 122 120 36 18 '#DDEEFF'
FillRound $g 1296 122 120 36 10 '#3C2D2D' '#D06A57' 2; CenterText $g '暂停' 1296 122 120 36 18 '#FFDCD4'
FillRound $g 34 195 1020 650 24 '#182018' '#3A5E38' 2
Text $g '场域地图表面：设施槽位、驻军、资源点都在地图上可见' 60 218 22 '#D8E6C4' ([System.Drawing.FontStyle]::Bold)
$b=Brush '#263C25'; $g.FillEllipse($b,70,260,930,520); $b.Dispose(); $b=Brush '#1B2C1C'; $g.FillEllipse($b,205,330,650,360); $b.Dispose()
$p=PenC '#465B35' 5; $g.DrawBezier($p,80,700,300,600,620,650,960,340); $p.Dispose(); $p=PenC '#6D593B' 9; $g.DrawBezier($p,100,720,310,610,615,660,960,360); $p.Dispose()
DrawSlot $g 145 335 '北侧矿坑' '施工中' '#D6B46A' '矿场：剩余 1 日' 0.55
DrawSlot $g 430 265 '东侧高台' '完成' '#A7D37B' '防御塔 Lv1｜防御 +2'
DrawSlot $g 705 455 '南侧营地' '空槽' '#8EB4D9' '可建造：兵营/仓库'
DrawSlot $g 360 570 '旧矿井' '受损' '#D0694B' '受损：产出 -50%'
FillRound $g 150 620 130 42 12 '#24344A' '#84B6EF' 2; CenterText $g '驻军 4｜英雄 1' 150 620 130 42 16 '#EAF4FF'
FillRound $g 785 305 150 42 12 '#4B3424' '#E5A65F' 2; CenterText $g '资源点：裸露石脉' 785 305 150 42 15 '#FFE4B8'
FillRound $g 1080 195 488 650 22 '#1C1D22' '#59462A' 2
Text $g '右侧检查器：选中“北侧矿坑”' 1110 222 23 '#F4E9C9' ([System.Drawing.FontStyle]::Bold)
Text $g '矿场施工中' 1110 270 30 '#F1D487' ([System.Drawing.FontStyle]::Bold)
Text $g '剩余：1 日' 1110 318 21 '#E8DBC0'; Text $g '已投入：经济 2，人口 1' 1110 352 21 '#E8DBC0'; Text $g '完成后：石材 +3 / 日' 1110 386 21 '#E8DBC0'
Text $g '战斗影响：无直接战斗加成；被 Raid 破坏后会降低产出。' 1110 430 18 '#B9AA83' ([System.Drawing.FontStyle]::Regular) 410 60
FillRound $g 1110 515 185 46 12 '#4C3A20' '#D6B46A' 2; CenterText $g '暂停施工' 1110 515 185 46 20 '#FFEFC6'
FillRound $g 1320 515 185 46 12 '#422829' '#D06A57' 2; CenterText $g '拆除工地' 1320 515 185 46 20 '#FFD8D2'
Text $g '时间反馈' 1110 610 22 '#F4E9C9' ([System.Drawing.FontStyle]::Bold)
FillRound $g 1110 650 410 95 14 '#121316' '#39342B' 1; Text $g '第 13 日：矿场施工进度 +1' 1130 668 18 '#E6D3A5'; Text $g '第 14 日：矿场建成，石材产出开启' 1130 698 18 '#A7D37B'
FillRound $g 34 870 1534 92 18 '#15171C' '#51452F' 2
Text $g '关键直觉：进场域 = 暂停规划；按“推进一日” = 同一个 WorldTick 在场域内结算。' 60 893 24 '#F4E9C9' ([System.Drawing.FontStyle]::Bold)
Text $g '战斗开始后隐藏经营 UI，WorldClock 冻结；战斗回合不会推动施工或产出。' 60 930 20 '#D6B46A'
$bmp.Save((Join-Path $outDir '01-site-management-overview.png'),[System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()

# 02 states
$pair=Canvas 1600 1000 '设施槽位状态视觉：空槽、施工、完成、受损、停用' '每个状态都要在地图上直接读懂，不只依赖右侧菜单文字。'
$bmp=$pair[0]; $g=$pair[1]
$states=@(@('空槽位','地面轮廓 + 木桩/旗帜','悬停显示可建造项；点击打开建造列表。','#8EB4D9','空槽'),@('施工中','脚手架 + 材料堆 + 进度条','每个 WorldTick 进度减少；完成时播放短反馈。','#D6B46A','施工中'),@('已完成','完整建筑 + 等级标签','显示产出/防御/驻军容量，不刷屏。','#A7D37B','完成'),@('受损','冒烟/裂纹/暗色遮罩','产出或战斗效果降低；行动优先提示修复。','#D0694B','受损'),@('停用/缺劳力','灰色遮罩 + 缺口图标','保留建筑实体，但产出显示为 0。','#8C8C8C','完成'))
for($i=0;$i -lt $states.Count;$i++){ $s=$states[$i]; $cx=90+($i%3)*500; $cy=155+[Math]::Floor($i/3)*350; FillRound $g $cx $cy 430 285 24 '#1B1D21' $s[3] 3; Text $g $s[0] ($cx+24) ($cy+22) 28 '#F5E5B7' ([System.Drawing.FontStyle]::Bold); DrawSlot $g ($cx+34) ($cy+78) '地图实体' $s[4] $s[3] $s[1] 0.5; Text $g $s[2] ($cx+220) ($cy+92) 20 '#E2D7BC' ([System.Drawing.FontStyle]::Regular) 170 120; FillRound $g ($cx+220) ($cy+205) 165 42 10 '#22252A' $s[3] 2; CenterText $g '右侧详情同步' ($cx+220) ($cy+205) 165 42 17 '#EFE6CF' }
FillRound $g 70 845 1460 90 18 '#15171C' '#51452F' 2
Text $g '实现建议：槽位是 authored scene/resource，运行时代码只绑定状态、刷新标签和行动列表。' 100 870 24 '#F4E9C9' ([System.Drawing.FontStyle]::Bold)
Text $g '避免用业务代码 new 控件树；重复 UI 用模板场景，建筑视觉用 PackedScene / Sprite / Animation。' 100 905 20 '#D6B46A'
$bmp.Save((Join-Path $outDir '02-building-state-language.png'),[System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()

# 03 boundary
$pair=Canvas 1600 1000 '时间边界：WorldTick 经营时间 与 BattleTurn 战斗回合完全隔离' '避免“战斗打了几回合，建筑偷偷完工”这类反直觉问题。'
$bmp=$pair[0]; $g=$pair[1]
FillRound $g 60 150 430 640 22 '#172019' '#7FB069' 3; FillRound $g 585 150 430 640 22 '#201B17' '#D6B46A' 3; FillRound $g 1110 150 430 640 22 '#181D28' '#7FA8D8' 3
Text $g '场域经营态' 95 185 30 '#CFF2B9' ([System.Drawing.FontStyle]::Bold); Text $g '暂停规划 / 推进世界步' 95 230 22 '#E3EDD5'
Text $g '战斗切入' 620 185 30 '#F5D58A' ([System.Drawing.FontStyle]::Bold); Text $g '冻结经营，读取当前状态' 620 230 22 '#EDE0C0'
Text $g '战斗态' 1145 185 30 '#CDE2FF' ([System.Drawing.FontStyle]::Bold); Text $g '只推进 BattleTurn / AP' 1145 230 22 '#DDEBFF'
$left=@('WorldClock 可暂停/恢复','推进一日 => WorldTick +1','施工进度减少','设施产出结算','威胁推进并可自动暂停')
$mid=@('隐藏经营按钮','WorldClock 强制暂停','生成 BattleStartRequest','防御塔/驻军作为输入','不在战斗中继续施工')
$right=@('回合结束不推进 WorldTick','AP 不影响建筑','建筑可作为战斗对象/支援','伤害只记为战斗结果','结束后统一 BattleResult 回写')
for($i=0;$i -lt $left.Count;$i++){ Text $g ('• '+$left[$i]) 100 (300+$i*55) 23 '#EAF5DD'; Text $g ('• '+$mid[$i]) 625 (300+$i*55) 23 '#F1E2BE'; Text $g ('• '+$right[$i]) 1150 (300+$i*55) 23 '#E2EEFF' }
Arrow $g 495 470 580 470 '#D6B46A'; Arrow $g 1020 470 1105 470 '#D6B46A'
Arrow $g 1325 795 1325 875 '#7FA8D8'; Line $g 1325 875 280 875 '#7FA8D8' 3; Arrow $g 280 875 280 795 '#7FA8D8'
Text $g '战斗结束：BattleResult 回写驻军、建筑受损、归属、奖励；然后回到经营态，可选择 WorldTick +1' 360 845 22 '#DDEBFF'
FillRound $g 215 720 1180 72 18 '#271717' '#D06A57' 3; CenterText $g '禁止：BattleTurn 自动推进施工 / 产出 / Raid 倒计时' 215 720 1180 72 28 '#FFD7D2' ([System.Drawing.FontStyle]::Bold)
FillRound $g 90 910 1420 52 14 '#15171C' '#51452F' 2; CenterText $g '玩家直觉：经营时间由玩家在世界层推进；战斗回合只服务战斗。两者通过 BattleStartRequest / BattleResult 交接。' 90 910 1420 52 22 '#F4E9C9'
$bmp.Save((Join-Path $outDir '03-worldtick-battleturn-boundary.png'),[System.Drawing.Imaging.ImageFormat]::Png); $g.Dispose(); $bmp.Dispose()

Get-ChildItem $outDir -Filter '*.png' | Where-Object Name -eq 'chinese-test.png' | Remove-Item -Force
$privateFonts.Dispose()
