using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework;

namespace RadDefenceGame.Windows;

public class ParticleEngine
{
    private readonly Random _random;
    public Vector2 EmitterLocation { get; set; }
    private readonly List<Particle> _particles;
    private readonly List<Texture2D> _textures;

    public ParticleEngine(List<Texture2D> textures, Vector2 location)
    {
        EmitterLocation = location;
        _textures = textures;
        _particles = new List<Particle>();
        _random = new Random();
    }

    private Particle GenerateNewParticle()
    {
        Texture2D texture = _textures[_random.Next(_textures.Count)];
        Vector2 position = EmitterLocation;
        Vector2 velocity = new(
                1f * (float)(_random.NextDouble() * 2 - 1),
                1f * (float)(_random.NextDouble() * 2 - 1));
        float angle = 0;
        float angularVelocity = 0.1f * (float)(_random.NextDouble() * 2 - 1);
        Color color = new(
                (float)_random.NextDouble(),
                (float)_random.NextDouble(),
                (float)_random.NextDouble());
        float size = (float)_random.NextDouble();
        int ttl = 20 + _random.Next(40);

        return new Particle(texture, position, velocity, angle, angularVelocity, color, size, ttl);
    }

    public void Update()
    {
        int total = 10;

        for (int i = 0; i < total; i++)
        {
            _particles.Add(GenerateNewParticle());
        }

        for (int particle = 0; particle < _particles.Count; particle++)
        {
            _particles[particle].Update();
            if (_particles[particle].TTL <= 0)
            {
                _particles.RemoveAt(particle);
                particle--;
            }
        }
    }

    public void Draw(SpriteBatch spriteBatch)
    {
        spriteBatch.Begin();
        for (int index = 0; index < _particles.Count; index++)
        {
            _particles[index].Draw(spriteBatch);
        }
        spriteBatch.End();
    }
}
