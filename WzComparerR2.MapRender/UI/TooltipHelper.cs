﻿using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Xna.Framework;
using WzComparerR2.Common;
using WzComparerR2.Rendering;

namespace WzComparerR2.MapRender.UI
{
    public static class TooltipHelper
    {
        public static TextBlock PrepareTextBlock(XnaFont font, string text, ref Vector2 pos, Color color)
        {
            Vector2 size = font.MeasureString(text);

            TextBlock block = new TextBlock();
            block.Font = font;
            block.Text = text;
            block.Position = pos;
            block.ForeColor = color;
           
            pos.X += size.X;
            return block;
        }

        public static TextBlock PrepareTextLine(XnaFont font, string text, ref Vector2 pos, Color color, ref float maxWidth)
        {
            Vector2 size = font.MeasureString(text);

            TextBlock block = new TextBlock();
            block.Font = font;
            block.Text = text;
            block.Position = pos;
            block.ForeColor = color;

            maxWidth = Math.Max(pos.X + size.X, maxWidth);
            pos.X = 0;
            pos.Y += font.Height;

            if (size.Y >= font.Height)
            {
                pos.Y += size.Y - font.BaseFont.Size;
            }

            return block;
        }

        public static TextBlock[] Prepare(LifeInfo info, MapRenderFonts fonts, out Vector2 size)
        {
            var blocks = new List<TextBlock>();
            var current = Vector2.Zero;
            size = Vector2.Zero;

            blocks.Add(PrepareTextLine(fonts.TooltipContentFont, "레벨: " + info.level + (info.boss ? " (Boss)" : null), ref current, Color.White, ref size.X));
            blocks.Add(PrepareTextLine(fonts.TooltipContentFont, "HP/MP: " + info.maxHP + " / " + info.maxMP, ref current, Color.White, ref size.X));
            blocks.Add(PrepareTextLine(fonts.TooltipContentFont, "물리/마법공격력: " + info.PADamage + " / " + info.MADamage, ref current, Color.White, ref size.X));
            blocks.Add(PrepareTextLine(fonts.TooltipContentFont, "물리/마법방어율: " + info.PDRate + "% / " + info.MDRate + "%", ref current, Color.White, ref size.X));
            blocks.Add(PrepareTextLine(fonts.TooltipContentFont, "회피/명중치: " + info.acc + " / " + info.eva, ref current, Color.White, ref size.X));
            blocks.Add(PrepareTextLine(fonts.TooltipContentFont, "넉백: " + info.pushed, ref current, Color.White, ref size.X));
            blocks.Add(PrepareTextLine(fonts.TooltipContentFont, "경험치: " + info.exp, ref current, Color.White, ref size.X));
            if (info.undead) blocks.Add(PrepareTextLine(fonts.TooltipContentFont, "undead: 1", ref current, Color.White, ref size.X));
            StringBuilder sb;
            if ((sb = GetLifeElemAttrString(ref info.elemAttr)).Length > 0)
                blocks.Add(PrepareTextLine(fonts.TooltipContentFont, "속성: " + sb.ToString(), ref current, Color.White, ref size.X));
            size.Y = current.Y;

            return blocks.ToArray();
        }

        public static StringBuilder GetLifeElemAttrString(ref LifeInfo.ElemAttr elemAttr)
        {
            StringBuilder sb = new StringBuilder(14);
            sb.Append(GetElemResistanceString("얼음", elemAttr.I));
            sb.Append(GetElemResistanceString("번개", elemAttr.L));
            sb.Append(GetElemResistanceString("불", elemAttr.F));
            sb.Append(GetElemResistanceString("독", elemAttr.S));
            sb.Append(GetElemResistanceString("성", elemAttr.H));
            sb.Append(GetElemResistanceString("암흑", elemAttr.D));
            sb.Append(GetElemResistanceString("물리", elemAttr.P));
            return sb;
        }

        public static string GetElemResistanceString(string elemName, LifeInfo.ElemResistance resist)
        {
            string e = null;
            switch (resist)
            {
                case LifeInfo.ElemResistance.Immune: e = "× "; break;
                case LifeInfo.ElemResistance.Resist: e = "△ "; break;
                case LifeInfo.ElemResistance.Normal: e = null; break;
                case LifeInfo.ElemResistance.Weak: e = "◎ "; break;
            }
            return e != null ? (elemName + e) : null;
        }

        public static string GetPortalTypeString(int pType)
        {
            switch (pType)
            {
                case 0: return "캐릭터시작지점";
                case 1: return "일반(숨겨짐)";
                case 2: return "일반";
                case 3: return "일반(접촉시활성)";
                case 6: return "워프게이트";
                case 7: return "스크립트";
                case 8: return "스크립트(숨겨짐)";
                case 9: return "스크립트(접촉시활성)";
                case 10: return "블링크";
                case 12: return "弹力装置";
                default: return null;
            }
        }

        public struct TextBlock
        {
            public Vector2 Position;
            public Color ForeColor;
            public XnaFont Font;
            public string Text;
        }
    }
}
