@echo off
chcp 65001 >nul
echo 正在取消 Haru 技能目录下 .asset 文件的只读属性...
attrib -R "%~dp0..\Assets\_Projects\Data\Skills\Priest\Haru\*.*" /S /D
attrib -R "%~dp0..\Assets\_Projects\Data\Skills\Priest\SkillPool\*.*" /S /D
echo.
echo 完成。请先完全关闭 Unity 和 Cursor，再只打开 Unity 试一次。
echo 若仍报错，请把项目移出「虚拟c盘」到普通路径（如 D:\Projects\PilgrimsOde）。
pause
