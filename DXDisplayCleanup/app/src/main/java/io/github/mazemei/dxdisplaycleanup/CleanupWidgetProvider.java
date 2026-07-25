package io.github.mazemei.dxdisplaycleanup;

import android.app.PendingIntent;
import android.appwidget.AppWidgetManager;
import android.appwidget.AppWidgetProvider;
import android.content.ComponentName;
import android.content.Context;
import android.content.Intent;
import android.widget.RemoteViews;

public final class CleanupWidgetProvider extends AppWidgetProvider {
    private static final String ACTION_REFRESH =
            "io.github.mazemei.dxdisplaycleanup.action.REFRESH";
    private static final String ACTION_CLEANUP =
            "io.github.mazemei.dxdisplaycleanup.action.CLEANUP";

    @Override
    public void onUpdate(Context context, AppWidgetManager manager, int[] ids) {
        for (int id : ids) {
            manager.updateAppWidget(id, buildViews(context));
        }
    }

    @Override
    public void onReceive(Context context, Intent intent) {
        super.onReceive(context, intent);
        String action = intent.getAction();
        if (ACTION_CLEANUP.equals(action)) {
            OverlayDisplayRepository.cleanup(context);
            CleanupTileService.requestRefresh(context);
            updateAll(context);
        } else if (ACTION_REFRESH.equals(action)) {
            updateAll(context);
        }
    }

    static void updateAll(Context context) {
        AppWidgetManager manager = AppWidgetManager.getInstance(context);
        ComponentName component = new ComponentName(
                context, CleanupWidgetProvider.class);
        int[] ids = manager.getAppWidgetIds(component);
        for (int id : ids) {
            manager.updateAppWidget(id, buildViews(context));
        }
    }

    private static RemoteViews buildViews(Context context) {
        OverlayDisplayRepository.Snapshot snapshot =
                OverlayDisplayRepository.inspect(context);
        RemoteViews views = new RemoteViews(
                context.getPackageName(), R.layout.widget_cleanup);

        if (snapshot.status == OverlayDisplayRepository.Status.PERMISSION_REQUIRED) {
            views.setImageViewResource(R.id.widget_icon, R.drawable.ic_warning);
            views.setTextViewText(R.id.widget_status,
                    context.getString(R.string.widget_permission));
        } else if (snapshot.status == OverlayDisplayRepository.Status.ERROR) {
            views.setImageViewResource(R.id.widget_icon, R.drawable.ic_warning);
            views.setTextViewText(R.id.widget_status,
                    context.getString(R.string.widget_error));
        } else if (snapshot.overlayActive) {
            views.setImageViewResource(R.id.widget_icon, R.drawable.dx_manager_icon);
            views.setTextViewText(R.id.widget_status,
                    context.getString(R.string.widget_active));
        } else {
            views.setImageViewResource(R.id.widget_icon, R.drawable.dx_manager_icon_mono);
            views.setTextViewText(R.id.widget_status,
                    context.getString(R.string.widget_inactive));
        }

        views.setOnClickPendingIntent(
                R.id.widget_icon,
                broadcastIntent(context, ACTION_REFRESH, 100));
        views.setOnClickPendingIntent(
                R.id.widget_refresh,
                broadcastIntent(context, ACTION_REFRESH, 101));
        views.setOnClickPendingIntent(
                R.id.widget_cleanup,
                broadcastIntent(context, ACTION_CLEANUP, 102));
        return views;
    }

    private static PendingIntent broadcastIntent(
            Context context, String action, int requestCode) {
        Intent intent = new Intent(context, CleanupWidgetProvider.class)
                .setAction(action);
        return PendingIntent.getBroadcast(
                context,
                requestCode,
                intent,
                PendingIntent.FLAG_UPDATE_CURRENT | PendingIntent.FLAG_IMMUTABLE);
    }
}
