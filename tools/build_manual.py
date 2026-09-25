"""Build the bundled PDF from the same XML consumed by the WPF help window.
Requires Python 3, reportlab and DejaVu Sans fonts (set NGRAPH_FONT_DIR if needed).
"""
from pathlib import Path
from xml.sax.saxutils import escape
import os
import re
import xml.etree.ElementTree as ET
from reportlab.pdfbase import pdfmetrics
from reportlab.pdfbase.ttfonts import TTFont
from reportlab.lib import colors
from reportlab.lib.styles import ParagraphStyle
from reportlab.lib.enums import TA_LEFT
from reportlab.lib.pagesizes import A4
from reportlab.platypus import BaseDocTemplate, PageTemplate, Frame, Paragraph, Spacer, PageBreak, CondPageBreak, KeepTogether
from reportlab.platypus.tableofcontents import TableOfContents

REPO = Path(__file__).resolve().parents[1]
source = REPO / 'NGraph/Resources/Help/Commands.xml'
target = REPO / 'NGraph/Resources/Help/NGraph_User_Guide.pdf'
font_dir = Path(os.environ.get('NGRAPH_FONT_DIR', '/usr/share/fonts/truetype/dejavu'))
for name, file in [('Guide', 'DejaVuSans.ttf'), ('GuideBold', 'DejaVuSans-Bold.ttf')]:
    pdfmetrics.registerFont(TTFont(name, str(font_dir / file)))
pdfmetrics.registerFontFamily('Guide', normal='Guide', bold='GuideBold', italic='Guide', boldItalic='GuideBold')
ink = colors.HexColor('#18263B'); muted = colors.HexColor('#52627A'); blue = colors.HexColor('#245BCC')
styles = {
 'body': ParagraphStyle('body', fontName='Guide', fontSize=9.7, leading=14.5, textColor=ink, spaceAfter=8, allowWidows=0, allowOrphans=0),
 'intro': ParagraphStyle('intro', fontName='Guide', fontSize=10.4, leading=15.5, textColor=muted, spaceAfter=13),
 'h1': ParagraphStyle('h1', fontName='GuideBold', fontSize=19, leading=25, textColor=ink, spaceBefore=15, spaceAfter=10, keepWithNext=True),
 'h2': ParagraphStyle('h2', fontName='GuideBold', fontSize=11.3, leading=16, textColor=blue, spaceBefore=8, spaceAfter=5, keepWithNext=True),
 'eyebrow': ParagraphStyle('eyebrow', fontName='GuideBold', fontSize=9, leading=14, textColor=blue, spaceAfter=8, keepWithNext=True),
 'cover': ParagraphStyle('cover', fontName='GuideBold', fontSize=34, leading=44, textColor=ink, spaceAfter=20),
}
def p(text, style='body'):
    # Paths remain searchable; allow line breaks after path separators.
    return Paragraph(escape(text).replace('\\', '\\<wbr/>'), styles[style])

root=ET.parse(source).getroot()
# Fail if a newly added ribbon command is undocumented.
ribbon=(REPO/'NGraph/Application.cs').read_text(encoding='utf-8-sig')
commands=set(re.findall(r'AddPushButton<([^>]+)>',ribbon))
documented={t.get('command') for t in root.findall('Topic') if t.get('command')}
assert commands == documented, f'Ribbon coverage mismatch: {commands ^ documented}'

class Manual(BaseDocTemplate):
    def afterFlowable(self, flowable):
        if hasattr(flowable, 'topic_id'):
            title=flowable.getPlainText()
            self.canv.bookmarkPage(flowable.topic_id)
            self.canv.addOutlineEntry(title, flowable.topic_id, level=0)
            self.notify('TOCEntry',(0,title,self.page,flowable.topic_id))
    def page_frame(self, canvas, doc):
        canvas.saveState()
        w,h=A4
        if doc.page > 1:
            canvas.setStrokeColor(colors.HexColor('#D5DDE9'));canvas.setLineWidth(.6);canvas.line(42,h-36,w-42,h-36)
            canvas.setFont('GuideBold',8);canvas.setFillColor(blue);canvas.drawString(42,h-27,'NGRAPH')
            canvas.setFont('Guide',8);canvas.setFillColor(muted);canvas.drawRightString(w-42,h-27,'Руководство пользователя · Revit 2021–2027')
        canvas.setFont('Guide',8);canvas.setFillColor(muted)
        canvas.drawString(42,25,'Редакция: '+root.get('edition'))
        canvas.drawRightString(w-42,25,str(doc.page))
        canvas.restoreState()

doc=Manual(str(target),pagesize=A4,leftMargin=42,rightMargin=42,topMargin=50,bottomMargin=45,
           title=root.get('title'),author='NGraph',subject='Руководство по всем командам NGraph для Revit',pageCompression=1)
frame=Frame(doc.leftMargin,doc.bottomMargin,doc.width,doc.height,id='body',leftPadding=0,rightPadding=0,topPadding=0,bottomPadding=0)
doc.addPageTemplates(PageTemplate(id='manual',frames=[frame],onPage=doc.page_frame))
story=[Spacer(1,62),p('NGRAPH / AUTODESK REVIT 2021–2027','eyebrow'),p('Руководство<br/>пользователя'.replace('<br/>','\n'),'cover')]
# Paragraph handles explicit line break on the cover only.
story[-1]=Paragraph('Руководство<br/>пользователя',styles['cover'])
story.extend([p('Схемы автоматизации · структурные схемы · листы · ХОВС','intro'),Spacer(1,24)])
for title,text in [
 ('01  Подготовка проекта','Установка, семейства, два вида базы и соответствия INI. Настройки сохраняются для текущего пользователя.'),
 ('02  Работа с командами',f'Пошаговые инструкции для всех {len(commands)} команд ленты: что подготовить, как запустить и что проверить в результате.'),
 ('03  История ХОВС','Импорт Excel, проверка установок, локальное обучение и сравнение нескольких сохранённых ревизий.')]:
    story.extend([p(title,'h2'),p(text),Spacer(1,12)])
story.extend([Spacer(1,24),p('Номер установленной программы смотрите в «NGraph → Справка → О программе». Редакция руководства описывает интерфейс и не заменяет номер сборки.','intro'),PageBreak(),p('Содержание','h1')])
toc=TableOfContents();toc.levelStyles=[ParagraphStyle('toc',fontName='Guide',fontSize=10,leading=18,textColor=ink,leftIndent=0,firstLineIndent=0,spaceBefore=0)]
story.extend([toc,PageBreak()])
for n,topic in enumerate(root.findall('Topic'),1):
    story.append(CondPageBreak(175))
    heading=p(f'{n:02d}  {topic.get("title")}', 'h1');heading.topic_id=topic.get('id')
    story.extend([heading,p(topic.get('group'),'eyebrow'),p(topic.findtext('Intro',''),'intro')])
    for section in topic.findall('Section'):
        story.append(p(section.get('title'),'h2'))
        for paragraph in section.findall('P'):story.append(p(paragraph.text or ''))
    if n < len(root): story.append(Spacer(1,10))
doc.multiBuild(story)
print(f'{target} ({target.stat().st_size:,} bytes); {len(commands)} ribbon commands, {len(root)} topics')
