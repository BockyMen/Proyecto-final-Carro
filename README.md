# Proyecto-final-Carro
Universidad de San Buenaventura, Ingeniería Multimedia.
Autores: William Aguilar, David Bolaños, Angela Buitrago y Andres Esquivia.

Vehículo de tres ruedas (tracción diferencial) basado en Arduino Uno con dos modos:
- *Automático:* evade obstáculos con un sensor ultrasónico HC-SR04 orientado por un servomotor SG90.
- *Manual (RC):* control remoto desde una interfaz en Unity por Bluetooth (HC-05).

Video: https://youtu.be/xti_RKyKaSo

## Contenido

- `Assets/CodeArduino/CodeCar.ino`: programa del vehículo (Arduino).
- `Assets/Scripts/CarController.cs`: script de Unity que controla el vehículo por Bluetooth.
- Raíz del repositorio: proyecto de Unity (`Assets/`, `Packages/`, `ProjectSettings/`).

## Hardware

Arduino Uno, controlador L293D (shield AFMotor), 2 motorreductores TT 1:48, rueda loca, HC-SR04, SG90, HC-05, 4 pilas AA con interruptor.

## Esquema de conexión

Montaje sobre un Arduino Uno con el shield controlador L293D (AFMotor V1) acoplado encima:

1. **Alimentación:** 4 pilas AA (6,0 V) con un interruptor en serie, conectadas a la entrada de alimentación externa del shield (`EXT_PWR`).
2. **Shield L293D:** se acopla directamente sobre el Arduino Uno. Alimenta con 5 V y GND los módulos HC-SR04 y HC-05.
3. **Motores DC (tipo TT):**
   1. Motor izquierdo → salida **M1** del shield.
   2. Motor derecho → salida **M3** del shield.
4. **Servomotor SG90:** conector **SERVO 1** del shield (pin 10 del Arduino). El HC-SR04 va montado sobre su eje, de modo que el sensor gira con él.
5. **Sensor ultrasónico HC-SR04:**
   1. VCC → 5 V
   2. GND → GND
   3. TRIG → **A0**
   4. ECHO → **A1**
6. **Módulo Bluetooth HC-05** (SoftwareSerial a 9600 baudios):
   1. VCC → 5 V
   2. GND → GND
   3. TXD (HC-05) → **A2** (RX del Arduino)
   4. RXD (HC-05) → **A3** (TX del Arduino)

## Cómo ejecutar

*Arduino*
1. Instala la biblioteca Adafruit Motor Shield Library V1 (AFMotor.h).
2. Carga Assets/CodeArduino/CodeCar.ino en el Arduino Uno y enciende el vehículo (arranca en modo manual).

*Unity*
1. Empareja el HC-05 en Windows (clave 1234 o 0000) y anota el puerto COM saliente.
2. Abre la carpeta del repositorio como proyecto desde Unity Hub (Unity 6000.5.9f1) y abre la escena `Assets/Scenes/InterfaceDelux`.
3. En Edit → Project Settings → Player → Other Settings, pon Api Compatibility Level en *.NET Framework*.
4. Selecciona en la escena el objeto que tiene el script `CarController` y escribe el puerto en el campo *Port Name* (Inspector). No hay que editar el código.
5. Cierra otros programas que usen ese puerto y ejecuta la escena.

## Controles y protocolo serial

9600 baudios, 8N1, un carácter ASCII por comando, enlace unidireccional (Unity → Arduino).

| Tecla | Carácter | Acción |
|---|---|---|
| W | F | Avanzar (se inhibe si d ≤ 25 cm) |
| S | B | Retroceder |
| A | L | Girar a la izquierda |
| D | R | Girar a la derecha |
| (ninguna) | S | Detener |
| F | A | Activar modo automático |
| C | S | Reconectar y volver a modo manual |

Failsafe: Unity reenvía el comando cada 150 ms; el Arduino detiene los motores tras 500 ms sin recibir ninguno.

## Modo automático

Avanza mientras la distancia frontal sea mayor de 25 cm. Si es menor o igual: frena, retrocede, mide a derecha (20°) e izquierda (160°) y decide (pared si ≤ 30 cm): gira hacia el lado libre, o da media vuelta si hay paredes en ambos lados. Los giros son por tiempo (lazo abierto).

## Limitaciones

Falla frente a paredes inclinadas (desvían el eco ultrasónico) y el vehículo se detiene tras unos 10 minutos de operación continua.