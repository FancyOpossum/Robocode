using Robocode.TankRoyale.BotApi;
using Robocode.TankRoyale.BotApi.Events;
using Robocode.TankRoyale.BotApi.Graphics;
using System;

// ------------------------------------------------------------------
// SHITSHOOTER 
// ------------------------------------------------------------------

public class ShitShooter : Bot
{
    private int _orbitDirection = 1;
    private Random _rng = new Random();
    private bool _hasTarget = false;

    static void Main(string[] args)
    {
        new ShitShooter().Start();
    }

    public override void Run()
    {
        BodyColor = Color.Brown;
        TurretColor = Color.Brown;
        RadarColor = Color.Brown;
        ScanColor = Color.Brown;

        AdjustRadarForBodyTurn = true;
        AdjustGunForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        // Start sweeping radar
        SetTurnRadarLeft(double.PositiveInfinity);

        // Always keep moving even if we see nobody
        SetForward(100);

        while (IsRunning)
        {
            // If we lose target, keep sweeping radar aggressively
            if (!_hasTarget)
                SetTurnRadarLeft(double.PositiveInfinity);

            Go();
            _hasTarget = false; // reset each tick unless we scan someone
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        _hasTarget = true;

        // -------- Radar Lock --------
        double bearing = RadarBearingTo(e.X, e.Y);
        double spread = Math.Atan(36.0 / DistanceTo(e.X, e.Y)) * (180.0 / Math.PI);
        double radarTurn = bearing + (bearing >= 0 ? spread : -spread);
        SetTurnRadarLeft(radarTurn);

        // -------- Predictive Firing --------
        PredictiveFire(e);

        // -------- Movement --------
        CalculateMovement(e);
    }

    private void PredictiveFire(ScannedBotEvent e)
    {
        double distance = DistanceTo(e.X, e.Y);
        double firePower = Math.Min(3.0, Math.Max(1.2, 500 / distance));
        double bulletSpeed = 20 - (3 * firePower);

        // Enemy heading & velocity
        double enemyHeadingRad = e.Direction * (Math.PI / 180.0);
        double enemyVelocity = e.Speed;

        // Current enemy position
        double enemyX = e.X;
        double enemyY = e.Y;

        // Time until bullet hits
        double deltaTime = 0;
        double predictedX = enemyX;
        double predictedY = enemyY;

        while ((++deltaTime) * bulletSpeed < DistanceTo(predictedX, predictedY))
        {
            predictedX += Math.Cos(enemyHeadingRad) * enemyVelocity;
            predictedY += Math.Sin(enemyHeadingRad) * enemyVelocity;

            // Stop prediction if outside arena
            if (predictedX < 18 || predictedY < 18 ||
                predictedX > ArenaWidth - 18 ||
                predictedY > ArenaHeight - 18)
            {
                predictedX = Math.Min(Math.Max(18, predictedX), ArenaWidth - 18);
                predictedY = Math.Min(Math.Max(18, predictedY), ArenaHeight - 18);
                break;
            }
        }

        double gunTurn = GunBearingTo(predictedX, predictedY);
        SetTurnGunLeft(gunTurn);

        if (Math.Abs(gunTurn) < 6 && GunHeat == 0)
            SetFire(firePower);
    }

    private void CalculateMovement(ScannedBotEvent e)
    {
        double distance = DistanceTo(e.X, e.Y);
        double angleToEnemy = DirectionTo(e.X, e.Y);

        double randomOffset = _rng.NextDouble() * 20 - 10;
        double goalDirection = angleToEnemy + (_orbitDirection * 90) + randomOffset;

        // Anti-ram control
        if (distance < 130)
        {
            goalDirection += _orbitDirection * 60;
            SetBack(80);
        }
        else
        {
            SetForward(120);
        }

        double smoothedDirection = WallSmooth(goalDirection);
        double turnAngle = CalcDeltaAngle(smoothedDirection, Direction);
        SetTurnLeft(turnAngle);
    }

    private double WallSmooth(double goalAngle)
    {
        double stickLength = 160;
        double margin = 28;

        for (int i = 0; i < 35; i++)
        {
            double angleRad = goalAngle * (Math.PI / 180.0);
            double projectedX = X + Math.Cos(angleRad) * stickLength;
            double projectedY = Y + Math.Sin(angleRad) * stickLength;

            bool safeX = projectedX > margin && projectedX < ArenaWidth - margin;
            bool safeY = projectedY > margin && projectedY < ArenaHeight - margin;

            if (safeX && safeY)
                return goalAngle;

            goalAngle -= _orbitDirection * 4;
        }

        return goalAngle;
    }

    public override void OnHitWall(HitWallEvent e)
    {
        _orbitDirection = -_orbitDirection;
        SetBack(100);
    }

    public override void OnHitBot(HitBotEvent e)
    {
        _orbitDirection = -_orbitDirection;
        SetBack(100);
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        if (_rng.NextDouble() > 0.5)
            _orbitDirection = -_orbitDirection;
    }
}