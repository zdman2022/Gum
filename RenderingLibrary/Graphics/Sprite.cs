﻿
using System;
using Microsoft.Xna.Framework.Graphics;
using RenderingLibrary.Content;
using System.Collections.ObjectModel;
using ToolsUtilitiesStandard.Helpers;
using MathHelper = ToolsUtilitiesStandard.Helpers.MathHelper;
using Vector2 = System.Numerics.Vector2;
using Color = System.Drawing.Color;
using Rectangle = System.Drawing.Rectangle;

namespace RenderingLibrary.Graphics
{
    public enum ColorOperation
    {
        //Texture,
        //Add,
        //Subtract,
        Modulate = 3,
        //InverseTexture,
        //Color,
        ColorTextureAlpha = 6,
        //Modulate2X,
        //Modulate4X,
        //InterpolateColor

    }
    public class Sprite : IRenderableIpso, IVisible, IAspectRatio
    {
        #region Fields

        Vector2 Position;
        IRenderableIpso mParent;

        ObservableCollection<IRenderableIpso> mChildren;

        public Color Color = Color.White;

        public int Alpha
        {
            get
            {
                return Color.A;
            }
            set
            {
                Color = Color.WithAlpha((byte)value);
            }
        }

        public int Red
        {
            get
            {
                return Color.R;
            }
            set
            {
                Color = Color.WithRed((byte)value);
            }
        }

        public int Green
        {
            get
            {
                return Color.G;
            }
            set
            {
                Color = Color.WithGreen((byte)value);
            }
        }

        public int Blue
        {
            get
            {
                return Color.B;
            }
            set
            {
                Color = Color.WithBlue((byte)value);
            }
        }

        public Rectangle? SourceRectangle;

        Texture2D mTexture;

        #endregion

        #region Properties

        // todo:  Anim sizing

        public string Name
        {
            get;
            set;
        }

        public float X
        {
            get { return Position.X; }
            set { Position.X = value; }
        }

        public float Y
        {
            get { return Position.Y; }
            set { Position.Y = value; }
        }

        public float Z
        {
            get;
            set;
        }

        public float EffectiveWidth
        {
            get
            {
                return Width;
            }
        }

        public float EffectiveHeight
        {
            get
            {
                // See comment in Width
                return Height;
            }
        }

        public float Width
        {
            get;
            set;
        }

        public float Height
        {
            get;
            set;
        }

        bool IRenderableIpso.ClipsChildren
        {
            get
            {
                return false;
            }
        }

        public IRenderableIpso Parent
        {
            get { return mParent; }
            set
            {
                if (mParent != value)
                {
                    if (mParent != null)
                    {
                        mParent.Children.Remove(this);
                    }
                    mParent = value;
                    if (mParent != null)
                    {
                        mParent.Children.Add(this);
                    }
                }
            }
        }

        float IPositionedSizedObject.Width
        {
            get
            {
                return EffectiveWidth;
            }
            set
            {
                Width = value;
            }
        }

        float IPositionedSizedObject.Height
        {
            get
            {
                return EffectiveHeight;
            }
            set
            {
                Height = value;
            }
        }

        public Texture2D Texture
        {
            get { return mTexture; }
            set
            {
                mTexture = value;
            }
        }

        public AtlasedTexture AtlasedTexture
        {
            get;
            set;
        }

        public IAnimation Animation
        {
            get;
            set;
        }

        public float Rotation { get; set; }

        public bool Animate
        {
            get;
            set;
        }

        public ObservableCollection<IRenderableIpso> Children
        {
            get { return mChildren; }
        }

        public object Tag { get; set; }

        public BlendState BlendState
        {
            get;
            set;
        }

        public ColorOperation ColorOperation { get; set; } = ColorOperation.Modulate;

        
        
        public bool FlipHorizontal
        {
            get;
            set;
        }

        public bool FlipVertical
        {
            get;
            set;
        }

        bool IRenderable.Wrap
        {
            get
            {
                return this.Wrap && mTexture != null &&
                    Math.MathFunctions.IsPowerOfTwo(mTexture.Width) &&
                    Math.MathFunctions.IsPowerOfTwo(mTexture.Height);

            }

        }

        public bool Wrap
        {
            get;
            set;
        }


        /// <summary>
        /// Returns the effective source rectangle, which may be the same as the SourceRectangle unless an AtlasedTexture is used.
        /// </summary>
        public Rectangle? EffectiveRectangle
        {
            get
            {
                Rectangle? sourceRectangle = SourceRectangle;

                if (AtlasedTexture != null)
                {
                    sourceRectangle = AtlasedTexture.SourceRectangle;

                    // Consider this.SourceRectangle to support rendering parts of a texture from a texture atlas:
                    if (this.SourceRectangle != null)
                    {
                        var toModify = sourceRectangle.Value;
                        toModify.X += this.SourceRectangle.Value.Left;
                        toModify.Y += this.SourceRectangle.Value.Top;

                        // We won't support wrapping (yet)
                        toModify.Width = System.Math.Min(toModify.Width, this.SourceRectangle.Value.Width);
                        toModify.Height = System.Math.Min(toModify.Height, this.SourceRectangle.Value.Height);

                        sourceRectangle = toModify;

                    }
                }

                return sourceRectangle;
            }
        }

        float IAspectRatio.AspectRatio => Texture != null ? (Texture.Width / (float)Texture.Height) : 1.0f;

        #endregion

        #region Methods

        public Sprite(Texture2D texture)
        {
            this.Visible = true;
            // why do we set this? It should be null so that 
            // the sprite will render using the default blendop, which may differ
            // depending on whether the game uses premult or standard
            //BlendState = BlendState.NonPremultiplied;
            mChildren = new ObservableCollection<IRenderableIpso>();

            Texture = texture;
        }

        public void AnimationActivity(double currentTime)
        {
            if (Animate)
            {
                Animation.AnimationActivity(currentTime);

                SourceRectangle = Animation.SourceRectangle;
                Texture = Animation.CurrentTexture;
                FlipHorizontal = Animation.FlipHorizontal;
                FlipVertical = Animation.FlipVertical;

                // Right now we'll just default this to resize the Sprite, but eventually we may want more control over it
                if (SourceRectangle.HasValue)
                {
                    this.Width = SourceRectangle.Value.Width;
                    this.Height = SourceRectangle.Value.Height;
                }
            }
        }

        void IRenderable.Render(SpriteRenderer spriteRenderer, SystemManagers managers)
        {
            if (this.AbsoluteVisible && Width > 0 && Height > 0)
            {
                bool shouldTileByMultipleCalls = this.Wrap && (this as IRenderable).Wrap == false;
                if (shouldTileByMultipleCalls && (this.Texture != null || this.AtlasedTexture != null))
                {
                    RenderTiledSprite(spriteRenderer, managers);
                }
                else
                {
                    Rectangle? sourceRectangle = EffectiveRectangle;
                    Texture2D texture = Texture;
                    if (AtlasedTexture != null)
                    {
                        texture = AtlasedTexture.Texture;
                    }

                    Render(managers, spriteRenderer, this, texture, Color, sourceRectangle, FlipVertical, this.GetAbsoluteRotation());
                }
            }
        }

        private void RenderTiledSprite(SpriteRenderer spriteRenderer, SystemManagers managers)
        {
            float texelsWide = 0;
            float texelsTall = 0;

            int fullTexelsWide = 0;
            int fullTexelsTall = 0;

            if (this.AtlasedTexture != null)
            {
                fullTexelsWide = this.AtlasedTexture.SourceRectangle.Width;
                fullTexelsTall = this.AtlasedTexture.SourceRectangle.Height;
            }
            else
            {
                fullTexelsWide = this.Texture.Width;
                fullTexelsTall = this.Texture.Height;
            }

            texelsWide = fullTexelsWide;
            if (SourceRectangle.HasValue)
            {
                texelsWide = SourceRectangle.Value.Width;
            }
            texelsTall = fullTexelsTall;
            if (SourceRectangle.HasValue)
            {
                texelsTall = SourceRectangle.Value.Height;
            }


            float xRepetitions = texelsWide / (float)fullTexelsWide;
            float yRepetitions = texelsTall / (float)fullTexelsTall;


            if (xRepetitions > 0 && yRepetitions > 0)
            {
                float eachWidth = this.EffectiveWidth / xRepetitions;
                float eachHeight = this.EffectiveHeight / yRepetitions;

                float oldEffectiveWidth = this.EffectiveWidth;
                float oldEffectiveHeight = this.EffectiveHeight;

                // We're going to change the width, height, X, and Y of "this" to make rendering code work
                // by simply passing in the object. At the end of the drawing, we'll revert the values back
                // to what they were before rendering started.
                float oldWidth = this.Width;
                float oldHeight = this.Height;

                float oldX = this.X;
                float oldY = this.Y;

                var oldSource = this.SourceRectangle.Value;


                float texelsPerWorldUnitX = (float)fullTexelsWide / eachWidth;
                float texelsPerWorldUnitY = (float)fullTexelsTall / eachHeight;

                int oldSourceY = oldSource.Y;

                if (oldSourceY < 0)
                {
                    int amountToAdd = 1 - (oldSourceY / fullTexelsTall);

                    oldSourceY += amountToAdd * Texture.Height;
                }

                if (oldSourceY > 0)
                {
                    int amountToAdd = System.Math.Abs(oldSourceY) / fullTexelsTall;
                    oldSourceY -= amountToAdd * Texture.Height;
                }
                float currentY = -oldSourceY * (1 / texelsPerWorldUnitY);

                var matrix = this.GetRotationMatrix();

                for (int y = 0; y < yRepetitions; y++)
                {
                    float worldUnitsChoppedOffTop = System.Math.Max(0, oldSourceY * (1 / texelsPerWorldUnitY));
                    //float worldUnitsChoppedOffBottom = System.Math.Max(0, currentY + eachHeight - (int)oldEffectiveHeight);

                    float worldUnitsChoppedOffBottom = 0;

                    float extraY = yRepetitions - y;
                    if (extraY < 1)
                    {
                        worldUnitsChoppedOffBottom = System.Math.Max(0, (1 - extraY) * eachHeight);
                    }



                    int texelsChoppedOffTop = 0;
                    if (worldUnitsChoppedOffTop > 0)
                    {
                        texelsChoppedOffTop = oldSourceY;
                    }

                    int texelsChoppedOffBottom =
                        RenderingLibrary.Math.MathFunctions.RoundToInt(worldUnitsChoppedOffBottom * texelsPerWorldUnitY);

                    int sourceHeight = (int)(fullTexelsTall - texelsChoppedOffTop - texelsChoppedOffBottom);

                    if (sourceHeight == 0)
                    {
                        break;
                    }

                    this.Height = sourceHeight * 1 / texelsPerWorldUnitY;

                    int oldSourceX = oldSource.X;

                    if (oldSourceX < 0)
                    {
                        int amountToAdd = 1 - (oldSourceX / Texture.Width);

                        oldSourceX += amountToAdd * fullTexelsWide;
                    }

                    if (oldSourceX > 0)
                    {
                        int amountToAdd = System.Math.Abs(oldSourceX) / Texture.Width;

                        oldSourceX -= amountToAdd * fullTexelsWide;
                    }

                    float currentX = -oldSourceX * (1 / texelsPerWorldUnitX) + y * eachHeight * matrix.Up().X;
                    currentY = y * eachHeight * matrix.Up().Y;

                    for (int x = 0; x < xRepetitions; x++)
                    {
                        float worldUnitsChoppedOffLeft = System.Math.Max(0, oldSourceX * (1 / texelsPerWorldUnitX));
                        float worldUnitsChoppedOffRight = 0;

                        float extra = xRepetitions - x;
                        if (extra < 1)
                        {
                            worldUnitsChoppedOffRight = System.Math.Max(0, (1 - extra) * eachWidth);
                        }

                        int texelsChoppedOffLeft = 0;
                        if (worldUnitsChoppedOffLeft > 0)
                        {
                            // Let's use the hard number to not have any floating point issues:
                            //texelsChoppedOffLeft = worldUnitsChoppedOffLeft * texelsPerWorldUnit;
                            texelsChoppedOffLeft = oldSourceX;
                        }
                        int texelsChoppedOffRight =
                            RenderingLibrary.Math.MathFunctions.RoundToInt(worldUnitsChoppedOffRight * texelsPerWorldUnitX);

                        this.X = oldX + currentX + worldUnitsChoppedOffLeft;
                        this.Y = oldY + currentY + worldUnitsChoppedOffTop;

                        int sourceWidth = (int)(fullTexelsWide - texelsChoppedOffLeft - texelsChoppedOffRight);

                        if (sourceWidth == 0)
                        {
                            break;
                        }

                        this.Width = sourceWidth * 1 / texelsPerWorldUnitX;




                        if (AtlasedTexture != null)
                        {
                            var rectangle = new Rectangle(
                                AtlasedTexture.SourceRectangle.X + texelsChoppedOffLeft,
                                AtlasedTexture.SourceRectangle.Y + texelsChoppedOffTop,
                                sourceWidth,
                                sourceHeight);

                            Render(managers, spriteRenderer, this, AtlasedTexture.Texture, Color, rectangle, FlipVertical, rotationInDegrees: Rotation);
                        }
                        else
                        {
                            this.SourceRectangle = new Rectangle(
                                texelsChoppedOffLeft,
                                texelsChoppedOffTop,
                                sourceWidth,
                                sourceHeight);

                            Render(managers, spriteRenderer, this, Texture, Color, SourceRectangle, FlipVertical, rotationInDegrees: Rotation);
                        }
                        currentX = System.Math.Max(0, currentX);
                        currentX += this.Width * matrix.Right().X;
                        currentY += this.Width * matrix.Right().Y;

                    }
                }

                this.Width = oldWidth;
                this.Height = oldHeight;

                this.X = oldX;
                this.Y = oldY;

                this.SourceRectangle = oldSource;
            }
        }



        public static void Render(SystemManagers managers, SpriteRenderer spriteRenderer, IRenderableIpso ipso, Texture2D texture)
        {
            Color color = Color.White;

            Render(managers, spriteRenderer, ipso, texture, color);
        }


        public static void Render(SystemManagers managers, SpriteRenderer spriteRenderer,
            IRenderableIpso ipso, Texture2D texture, Color color,
            Rectangle? sourceRectangle = null,
            bool flipVertical = false,
            float rotationInDegrees = 0,
            bool treat0AsFullDimensions = false,
            // In the case of Text objects, we send in a line rectangle, but we want the Text object to be the owner of any resulting render states
            object objectCausingRendering = null
            )
        {
            if (objectCausingRendering == null)
            {
                objectCausingRendering = ipso;
            }

            Renderer renderer = null;
            if (managers == null)
            {
                renderer = Renderer.Self;
            }
            else
            {
                renderer = managers.Renderer;
            }

            Texture2D textureToUse = texture;

            if (textureToUse == null)
            {
                textureToUse = LoaderManager.Self.InvalidTexture;

                if (textureToUse == null)
                {
                    return;
                }
            }

            SpriteEffects effects = SpriteEffects.None;

            var flipHorizontal = ipso.GetAbsoluteFlipHorizontal();
            var effectiveParentFlipHorizontal = ipso.Parent?.GetAbsoluteFlipHorizontal() ?? false;

            if (flipHorizontal)
            {
                effects |= SpriteEffects.FlipHorizontally;
                //rotationInDegrees *= -1;
            }

            var rotationInRadians = MathHelper.ToRadians(rotationInDegrees);


            float leftAbsolute = ipso.GetAbsoluteX();
            float topAbsolute = ipso.GetAbsoluteY();

            Vector2 origin = Vector2.Zero;

            //if(flipHorizontal)
            //{
            //    var offsetX = (float)System.Math.Cos(rotationInRadians);
            //    var offsetY = (float)System.Math.Sin(rotationInRadians);
            //    origin.X = 1;

                

            //}

            if (flipVertical)
            {
                effects |= SpriteEffects.FlipVertically;
            }

            var modifiedColor = color;

            // Custom effect already does premultiply alpha on the shader so we skip that in this case
            if (!Renderer.UseCustomEffectRendering && Renderer.NormalBlendState == BlendState.AlphaBlend)
            {
                // we are using premult textures, so we need to premult the color:
                var alphaRatio = color.A / 255.0f;

                modifiedColor = Color.FromArgb(modifiedColor.A,
                    (byte)(color.R * alphaRatio),
                    (byte)(color.G * alphaRatio),
                    (byte)(color.B * alphaRatio));
            }

            if ((ipso.Width > 0 && ipso.Height > 0) || treat0AsFullDimensions == false)
            {
                Vector2 scale = Vector2.One;

                if (textureToUse == null)
                {
                    scale = new Vector2(ipso.Width, ipso.Height);
                }
                else
                {
                    float ratioWidth = 1;
                    float ratioHeight = 1;
                    if (sourceRectangle.HasValue)
                    {
                        ratioWidth = sourceRectangle.Value.Width / (float)textureToUse.Width;
                        ratioHeight = sourceRectangle.Value.Height / (float)textureToUse.Height;
                    }

                    scale = new Vector2(ipso.Width / (ratioWidth * textureToUse.Width),
                        ipso.Height / (ratioHeight * textureToUse.Height));

                    if(ratioWidth == 0)
                    {
                        scale.X = 0;
                    }
                    if(ratioHeight == 0)
                    {
                        scale.Y = 0;
                    }
                }

#if DEBUG
                if(float.IsPositiveInfinity( scale.X))
                {
                    throw new Exception("scale.X is positive infinity, it shouldn't be!");
                }

                if (textureToUse != null && textureToUse.IsDisposed)
                {
                    throw new ObjectDisposedException($"Texture is disposed.  Texture name: {textureToUse.Name}, sprite scale: {scale}, Sprite name: {ipso.Name}");
                }
#endif

                spriteRenderer.Draw(textureToUse,
                    new Vector2(leftAbsolute, topAbsolute),
                    sourceRectangle,
                    modifiedColor,
                    -rotationInRadians,
                    origin,
                    scale,
                    effects,
                    0,
                    objectCausingRendering, renderer);
            }
            else
            {
                int width = textureToUse.Width;
                int height = textureToUse.Height;

                if (sourceRectangle != null && sourceRectangle.HasValue)
                {
                    width = sourceRectangle.Value.Width;
                    height = sourceRectangle.Value.Height;
                }

                Rectangle destinationRectangle = new Rectangle(
                    (int)(leftAbsolute),
                    (int)(topAbsolute),
                    width,
                    height);


                spriteRenderer.Draw(textureToUse,
                    destinationRectangle,
                    sourceRectangle,
                    modifiedColor,
                    rotationInRadians,
                    origin,
                    effects,
                    0,
                    objectCausingRendering
                    );
            }
        }

        public override string ToString()
        {
            return Name + " (Sprite)";
        }

        #endregion

        void IRenderableIpso.SetParentDirect(IRenderableIpso parent)
        {
            mParent = parent;
        }

        void IRenderable.PreRender() { }

        #region IVisible Implementation

        public bool Visible
        {
            get;
            set;
        }

        public bool AbsoluteVisible
        {
            get
            {
                if (((IVisible)this).Parent == null)
                {
                    return Visible;
                }
                else
                {
                    return Visible && ((IVisible)this).Parent.AbsoluteVisible;
                }
            }
        }

        IVisible IVisible.Parent
        {
            get
            {
                return ((IRenderableIpso)this).Parent as IVisible;
            }
        }

        #endregion


    }
}
