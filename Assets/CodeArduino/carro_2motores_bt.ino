#include <AFMotor.h>
#include <Servo.h>
#include <SoftwareSerial.h>

#define TRIG A0
#define ECHO A1
#define BT_RX A2
#define BT_TX A3
#define SERVO_PIN 10

#define SPEED 250
#define MANEUVER_SPEED 180
#define BRAKE_TIME 150
#define SERVO_CENTER 90
#define SERVO_RIGHT 20
#define SERVO_LEFT 160
#define STOP_DISTANCE 25
#define SIDE_DISTANCE 30
#define TURN_TIME 400
#define BACK_TIME 300
#define FAILSAFE_MS 500

#define INVERT_LEFT false
#define INVERT_RIGHT true

SoftwareSerial bt(BT_RX, BT_TX);
Servo servo;
AF_DCMotor leftMotor(1);
AF_DCMotor rightMotor(3);

enum Mode { MANUAL, AUTO };
Mode mode = MANUAL;

char manualCommand = 'S';
char currentMove = '-';
unsigned long lastCommandTime = 0;

long measureOnce() {
  digitalWrite(TRIG, LOW);
  delayMicroseconds(4);
  digitalWrite(TRIG, HIGH);
  delayMicroseconds(10);
  digitalWrite(TRIG, LOW);
  unsigned long t = pulseIn(ECHO, HIGH, 30000UL);
  if (t == 0) return 999;
  return t / 58;
}

long measureDistance() {
  long a = measureOnce(); delay(10);
  long b = measureOnce(); delay(10);
  long c = measureOnce();
  if ((a <= b && b <= c) || (c <= b && b <= a)) return b;
  if ((b <= a && a <= c) || (c <= a && a <= b)) return a;
  return c;
}

long lookAt(int angle) {
  servo.write(angle);
  delay(500);
  return measureDistance();
}

void setSpeedAll(int s) {
  leftMotor.setSpeed(s);
  rightMotor.setSpeed(s);
}

void runMotor(AF_DCMotor &m, uint8_t dir, bool invert) {
  if (invert && dir == FORWARD)       dir = BACKWARD;
  else if (invert && dir == BACKWARD) dir = FORWARD;
  m.run(dir);
}

void forward() {
  runMotor(leftMotor, FORWARD, INVERT_LEFT);
  runMotor(rightMotor, FORWARD, INVERT_RIGHT);
}

void backward() {
  runMotor(leftMotor, BACKWARD, INVERT_LEFT);
  runMotor(rightMotor, BACKWARD, INVERT_RIGHT);
}

void turnLeft() {
  runMotor(leftMotor, BACKWARD, INVERT_LEFT);
  runMotor(rightMotor, FORWARD, INVERT_RIGHT);
}

void turnRight() {
  runMotor(leftMotor, FORWARD, INVERT_LEFT);
  runMotor(rightMotor, BACKWARD, INVERT_RIGHT);
}

void stopMotors() {
  leftMotor.run(RELEASE);
  rightMotor.run(RELEASE);
}

void brake() {
  stopMotors();
  delay(BRAKE_TIME);
}

void applyMove(char c) {
  if (c == currentMove) return;

  bool wasMoving = currentMove == 'F' || currentMove == 'B' || currentMove == 'L' || currentMove == 'R';
  bool willMove = c == 'F' || c == 'B' || c == 'L' || c == 'R';
  if (wasMoving && willMove) brake();

  setSpeedAll(c == 'F' ? SPEED : MANEUVER_SPEED);

  switch (c) {
    case 'F': forward();   break;
    case 'B': backward();  break;
    case 'L': turnLeft();  break;
    case 'R': turnRight(); break;
    default:  stopMotors(); c = 'S'; break;
  }
  currentMove = c;
}

void avoidObstacle() {
  brake();
  currentMove = 'S';
  Serial.println("Obstacle ahead");

  setSpeedAll(MANEUVER_SPEED);
  backward();
  delay(BACK_TIME);
  brake();

  long right = lookAt(SERVO_RIGHT);
  long left = lookAt(SERVO_LEFT);
  servo.write(SERVO_CENTER);
  delay(300);

  Serial.print("Right: ");
  Serial.print(right);
  Serial.print(" cm | Left: ");
  Serial.print(left);
  Serial.println(" cm");

  bool rightBlocked = right <= SIDE_DISTANCE;
  bool leftBlocked = left <= SIDE_DISTANCE;

  if (rightBlocked && !leftBlocked) {
    Serial.println("Wall on the right, turning left");
    turnLeft();
    delay(TURN_TIME);
  } else if (leftBlocked && !rightBlocked) {
    Serial.println("Wall on the left, turning right");
    turnRight();
    delay(TURN_TIME);
  } else if (rightBlocked && leftBlocked) {
    Serial.println("Walls on both sides, turning around");
    backward();
    delay(BACK_TIME * 2);
    brake();
    turnRight();
    delay(TURN_TIME * 2);
  } else {
    if (right >= left) {
      Serial.println("Both sides clear, turning right");
      turnRight();
    } else {
      Serial.println("Both sides clear, turning left");
      turnLeft();
    }
    delay(TURN_TIME);
  }

  brake();
  currentMove = 'S';
  Serial.println("Resuming");
}

void autoLoop() {
  long d = measureDistance();
  if (d <= STOP_DISTANCE) avoidObstacle();
  else applyMove('F');
}

void manualLoop() {
  if (millis() - lastCommandTime > FAILSAFE_MS) manualCommand = 'S';

  char c = manualCommand;
  if (c == 'F' && measureDistance() <= STOP_DISTANCE) c = 'S';

  applyMove(c);
}

void receive(char c) {
  if (c >= 'a' && c <= 'z') c = c - 'a' + 'A';

  if (c == 'A') {
    if (mode != AUTO) {
      mode = AUTO;
      servo.write(SERVO_CENTER);
      Serial.println("AUTO MODE");
    }
    return;
  }

  if (c == 'F' || c == 'B' || c == 'L' || c == 'R' || c == 'S') {
    if (mode != MANUAL) {
      mode = MANUAL;
      stopMotors();
      currentMove = 'S';
      servo.write(SERVO_CENTER);
      Serial.println("MANUAL MODE");
    }
    manualCommand = c;
    lastCommandTime = millis();
  }
}

void setup() {
  Serial.begin(9600);
  bt.begin(9600);
  pinMode(TRIG, OUTPUT);
  pinMode(ECHO, INPUT);
  digitalWrite(TRIG, LOW);

  servo.attach(SERVO_PIN);
  servo.write(SERVO_CENTER);

  setSpeedAll(SPEED);
  stopMotors();

  Serial.println("Ready. MANUAL: F B L R S | AUTO: A");
}

void loop() {
  while (bt.available() > 0)     receive(bt.read());
  while (Serial.available() > 0) receive(Serial.read());

  if (mode == AUTO) autoLoop();
  else              manualLoop();

  delay(20);
}