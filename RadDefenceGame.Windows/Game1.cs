using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System.Collections.Generic;

namespace RadDefenceGame.Windows
{
    /// <summary>
    /// This is the main type for your game.
    /// </summary>
    public class Game1 : Game
    {
        GraphicsDeviceManager graphics;
        SpriteBatch spriteBatch;
        //texture2D objects
        private Texture2D background;
        private Texture2D shuttle;
        private Texture2D earth;

        private Texture2D arrow;
        private float angle = 0;

        //spritefonts
        private SpriteFont font;
        private int score = 0;
        //Texture Atlases
        private AnimatedSprite animatedSprite;

        ParticleEngine particleEngine;

        public Game1()
        {
            graphics = new GraphicsDeviceManager(this);
            Content.RootDirectory = "Content";
        }

        /// <summary>
        /// Allows the game to perform any initialization it needs to before starting to run.
        /// This is where it can query for any required services and load any non-graphic
        /// related content.  Calling base.Initialize will enumerate through any components
        /// and initialize them as well.
        /// </summary>
        protected override void Initialize()
        {
            // TODO: Add your initialization logic here

            base.Initialize();
        }

        /// <summary>
        /// LoadContent will be called once per game and is the place to load
        /// all of your content.
        /// </summary>
        protected override void LoadContent()
        {
            // Create a new SpriteBatch, which can be used to draw textures.
            spriteBatch = new SpriteBatch(GraphicsDevice);
            background = Content.Load<Texture2D>("Images\\stars");
            //shuttle = Content.Load<Texture2D>("Images\\shuttle");
            //earth = Content.Load<Texture2D>("Images\\earth");
            //font = Content.Load<SpriteFont>("Score"); // Use the name of your sprite font file here instead of 'Score'.
            //Texture2D texture = Content.Load<Texture2D>("TextureAtlases\\SmileyWalk");
            //animatedSprite = new AnimatedSprite(texture, 4, 4);

            //arrow = Content.Load<Texture2D>("Images\\arrow"); // use the name of your texture here, if you are using your own

            List<Texture2D> textures = new List<Texture2D>();
            textures.Add(Content.Load<Texture2D>("Images\\circle"));
            textures.Add(Content.Load<Texture2D>("Images\\star"));
            textures.Add(Content.Load<Texture2D>("Images\\diamond"));
            particleEngine = new ParticleEngine(textures, new Vector2(400, 240));

        }

        /// <summary>
        /// UnloadContent will be called once per game and is the place to unload
        /// game-specific content.
        /// </summary>
        protected override void UnloadContent()
        {
            // TODO: Unload any non ContentManager content here
        }

        /// <summary>
        /// Allows the game to run logic such as updating the world,
        /// checking for collisions, gathering input, and playing audio.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Update(GameTime gameTime)
        {
            if (GamePad.GetState(PlayerIndex.One).Buttons.Back == ButtonState.Pressed || Keyboard.GetState().IsKeyDown(Keys.Escape))
                Exit();

            //score++;
            //animatedSprite.Update();
            //angle += 0.01f;

            particleEngine.EmitterLocation = new Vector2(Mouse.GetState().X, Mouse.GetState().Y);
            particleEngine.Update();

            base.Update(gameTime);
        }

        /// <summary>
        /// This is called when the game should draw itself.
        /// </summary>
        /// <param name="gameTime">Provides a snapshot of timing values.</param>
        protected override void Draw(GameTime gameTime)
        {
            GraphicsDevice.Clear(Color.CornflowerBlue);

            spriteBatch.Begin();

            spriteBatch.Draw(background, new Rectangle(0, 0, 800, 600), Color.White);
            //spriteBatch.Draw(earth, new Vector2(400, 240), Color.White);
            //spriteBatch.Draw(shuttle, new Vector2(450, 240), Color.White);
            //spriteBatch.DrawString(font, "Score: " + score, new Vector2(100, 100), Color.Black);
            spriteBatch.End();

            //animatedSprite.Draw(spriteBatch, new Vector2(400, 200));

            //spriteBatch.Begin();

            //Vector2 location = new Vector2(400, 240);
            //Rectangle sourceRectangle = new Rectangle(0, 0, arrow.Width, arrow.Height);
            ////Vector2 origin = new Vector2(0, 0);
            //Vector2 origin = new Vector2(arrow.Width / 2, arrow.Height);

            //spriteBatch.Draw(arrow, location, sourceRectangle, Color.White, angle, origin, 1.0f, SpriteEffects.None, 1);

            //spriteBatch.End();



            base.Draw(gameTime);
        }
    }
}
