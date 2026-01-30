namespace SimpleBluetoothTerminalNet;

public interface ISerialListener
{
    void OnSerialConnect();
    void OnSerialConnectError(Exception e);
    void OnSerialRead(byte[] data);                    // socket -> service
    void OnSerialRead(Queue<byte[]> datas);            // service -> UI thread
    void OnSerialIoError(Exception e);
}
