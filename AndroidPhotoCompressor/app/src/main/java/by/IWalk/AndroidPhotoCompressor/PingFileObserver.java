package by.IWalk.AndroidPhotoCompressor;

import android.os.FileObserver;

import androidx.annotation.Nullable;

import java.io.File;
import java.io.IOException;

public class PingFileObserver extends FileObserver {
    final static String fileNameToObserve = "ping.txt";
    private final String folderForPingFile;

    public PingFileObserver(String folderForPingFile) {
        super(new File(folderForPingFile), CREATE);
        this.folderForPingFile = folderForPingFile;

        // трэба прыбраць на сваёй палове стала для гульні ў ping-pong
        File pingFile = new File(folderForPingFile, fileNameToObserve);
        if (pingFile.exists()) {
            if (!pingFile.delete()) {
                throw new NotImplementedException();
            }
        }
    }

    @Override
    public void onEvent(int i, @Nullable String s) {
        if (!fileNameToObserve.equals(s)) {
            // чакаем калі мяч апынецца на гэтай палове стала
            return;
        }

        // намах перад ударам па мячы
        File pingFile = new File(folderForPingFile, fileNameToObserve);
        if (!pingFile.delete()) {
            throw new NotImplementedException();
        }

        // удар!
        final String originalFileName = "original.jpg";
        PhotoCompressor.compressImage(folderForPingFile, originalFileName, "compressed.jpg");

        File originalFile = new File(folderForPingFile, originalFileName);
        if (!originalFile.delete()) {
            throw new NotImplementedException();
        }

        // мяч пералятае на бок партнёра
        File pongFile = new File(folderForPingFile, "pong.txt");
        try {
            if (!pongFile.createNewFile()) {
                throw new NotImplementedException();
            }
        } catch (IOException e) {
            throw new NotImplementedException(e);
        }
    }
}
