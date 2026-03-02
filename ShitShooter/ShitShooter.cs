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

    // Escape system
    private int _escapeTicks = 0;

    // Wall assurance system
    private int _wallEscapeTicks = 0;
    private int _lowSpeedTicks = 0;

    // Pattern tracking
    private double _lastEnemyHeading = 0;
    private double _lastEnemyVelocity = 0;
    private double _lastTurnRate = 0;

    private int _directionChanges = 0;
    private int _velocityChanges = 0;
    private int _stableTurnTicks = 0;

    static void Main(string[] args)
    {
        new ShitShooter().Start();
    }

    public override void Run()
    {
        BodyColor = Color.SaddleBrown;
        TurretColor = Color.SaddleBrown;
        RadarColor = Color.SaddleBrown;
        ScanColor = Color.SaddleBrown;
        BulletColor = Color.SaddleBrown;
        TracksColor = Color.Yellow;

        AdjustRadarForBodyTurn = true;
        AdjustGunForBodyTurn = true;
        AdjustRadarForGunTurn = true;

        SetTurnRadarLeft(double.PositiveInfinity);
        SetForward(100);

        while (IsRunning)
        {
            if (!_hasTarget)
                SetTurnRadarLeft(double.PositiveInfinity);

            WallEscapeAssurance(); 

            Go();
            _hasTarget = false;
        }
    }

    public override void OnScannedBot(ScannedBotEvent e)
    {
        _hasTarget = true;

        if (_escapeTicks > 0)
        {
            _escapeTicks--;
            return;
        }

        // Radar lock 
        double bearing = RadarBearingTo(e.X, e.Y);
        double spread = Math.Atan(36.0 / DistanceTo(e.X, e.Y)) * (180.0 / Math.PI);
        SetTurnRadarLeft(bearing + (bearing >= 0 ? spread : -spread));

        AnalyzeMovementPattern(e);
        AdvancedPredictiveFire(e);
        CalculateMovement(e);
    }

    // =========================
    // WALL ESCAPE
    // =========================
    private void WallEscapeAssurance()
    {
        double margin = 45;

        bool nearWall =
            X < margin ||
            X > ArenaWidth - margin ||
            Y < margin ||
            Y > ArenaHeight - margin;

        // Detect freeze (speed very low for several ticks)
        if (Math.Abs(Speed) < 0.5)
            _lowSpeedTicks++;
        else
            _lowSpeedTicks = 0;

        if (nearWall || _lowSpeedTicks > 10)
        {
            _wallEscapeTicks = 20;
        }

        if (_wallEscapeTicks > 0)
        {
            _wallEscapeTicks--;

            // Move toward arena center
            double centerX = ArenaWidth / 2;
            double centerY = ArenaHeight / 2;

            double escapeAngle = DirectionTo(centerX, centerY);
            double turn = CalcDeltaAngle(escapeAngle, Direction);

            SetTurnLeft(turn);
            SetForward(150);
        }
    }

    // =========================
    // Stop bot from being stuck
    // =========================
    public override void OnHitBot(HitBotEvent e)
    {
        _orbitDirection = -_orbitDirection;

        SetForward(0);
        SetBack(0);

        double escapeAngle = Direction + 180;
        double turnAmount = CalcDeltaAngle(escapeAngle, Direction);
        SetTurnLeft(turnAmount);

        SetForward(150);
        _escapeTicks = 15;
    }

    public override void OnHitWall(HitWallEvent e)
    {
        _orbitDirection = -_orbitDirection;
        SetBack(100);
    }

    public override void OnHitByBullet(HitByBulletEvent e)
    {
        if (_rng.NextDouble() > 0.5)
            _orbitDirection = -_orbitDirection;
    }

    // =========================
    // Analyze enemy movement to classify patterns and adjust targeting
    // =========================
    private void AnalyzeMovementPattern(ScannedBotEvent e)
    {
        double headingChange = NormalizeAngle(e.Direction - _lastEnemyHeading);
        double velocityChange = Math.Abs(e.Speed - _lastEnemyVelocity);
        double turnRate = headingChange;

        if (Math.Abs(headingChange) > 12)
            _directionChanges++;

        if (velocityChange > 2)
            _velocityChanges++;

        if (Math.Abs(turnRate - _lastTurnRate) < 1.5 && Math.Abs(e.Speed) > 3)
            _stableTurnTicks++;
        else
            _stableTurnTicks = 0;

        _lastTurnRate = turnRate;
        _lastEnemyHeading = e.Direction;
        _lastEnemyVelocity = e.Speed;
    }

    // =========================
    // Targeting system
    // =========================
    private void AdvancedPredictiveFire(ScannedBotEvent e)
    {
        double distance = DistanceTo(e.X, e.Y);
        double firePower = Math.Min(3.0, Math.Max(1.2, 500 / distance));
        double bulletSpeed = 20 - (3 * firePower);

        double predictedX = e.X;
        double predictedY = e.Y;

        double enemyHeadingRad = e.Direction * Math.PI / 180.0;
        double enemyVelocity = e.Speed;
        double turnRateRad = _lastTurnRate * Math.PI / 180.0;

        double deltaTime = 0;

        bool circular = _directionChanges > 3;
        bool jittery = _velocityChanges > 4;
        bool spinner = _stableTurnTicks > 6;

        while ((++deltaTime) * bulletSpeed < DistanceTo(predictedX, predictedY))
        {
            if (spinner)
                enemyHeadingRad += turnRateRad;
            else if (circular)
                enemyHeadingRad += turnRateRad * 0.8;

            predictedX += Math.Cos(enemyHeadingRad) * enemyVelocity;
            predictedY += Math.Sin(enemyHeadingRad) * enemyVelocity;

            if (predictedX < 18 || predictedY < 18 ||
                predictedX > ArenaWidth - 18 ||
                predictedY > ArenaHeight - 18)
            {
                predictedX = Math.Min(Math.Max(18, predictedX), ArenaWidth - 18);
                predictedY = Math.Min(Math.Max(18, predictedY), ArenaHeight - 18);
                break;
            }
        }

        if (jittery)
        {
            predictedX = e.X + (predictedX - e.X) * 0.6;
            predictedY = e.Y + (predictedY - e.Y) * 0.6;
            firePower = Math.Min(firePower, 2.0);
        }

        if (spinner)
            firePower = Math.Min(3.0, firePower + 0.5);

        double gunTurn = GunBearingTo(predictedX, predictedY);
        SetTurnGunLeft(gunTurn);

        if (Math.Abs(gunTurn) < 4 && GunHeat == 0)
            SetFire(firePower);
    }

    // =========================
    // Movement
    // =========================
    private void CalculateMovement(ScannedBotEvent e)
    {
        double distance = DistanceTo(e.X, e.Y);
        double angleToEnemy = DirectionTo(e.X, e.Y);

        double randomOffset = _rng.NextDouble() * 20 - 10;
        double goalDirection = angleToEnemy + (_orbitDirection * 90) + randomOffset;

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
            double angleRad = goalAngle * Math.PI / 180.0;
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

    private double NormalizeAngle(double angle)
    {
        while (angle > 180) angle -= 360;
        while (angle < -180) angle += 360;
        return angle;
    }
}